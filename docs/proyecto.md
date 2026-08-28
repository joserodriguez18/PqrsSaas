# Plataforma SaaS Multi-tenant de Gestión de PQRS con IA y Widget Incrustable

## 1. Planteamiento del Problema
Las pequeñas y medianas empresas (PyMEs) enfrentan dos cuellos de botella críticos en la gestión de su soporte y atención al cliente:
* **Dispersión y Pérdida de Trazabilidad:** Las peticiones, quejas, reclamos y sugerencias (PQRS) se reciben por canales heterogéneos (correos, redes sociales o formularios aislados), lo que genera retrasos en la atención, falta de visibilidad en el ciclo de vida de cada solicitud y descontento en los usuarios.
* **Saturación por Consultas Repetitivas y Triaje Manual Lento:** Un porcentaje alto de las solicitudes corresponden a preguntas frecuentes (FAQs) que podrían resolverse automáticamente. Además, las PQRS que sí requieren atención humana son clasificadas y priorizadas manualmente por agentes, introduciendo sesgos y retrasos en casos urgentes.

Para resolver esto, se requiere una plataforma SaaS multi-tenant que permita a múltiples empresas centralizar y automatizar sus PQRS. La solución ofrece un widget web incrustable de inserción dinámica que funciona en dos fases: en primera instancia como un asistente de auto-atención mediante Retrieval-Augmented Generation (RAG) basado en la documentación de la empresa, y en segunda instancia como un canal de radicación formal con un módulo de Inteligencia Artificial que realiza triaje, clasificación y priorización automática del ticket al ingresar.

## 2. Alcance (MVP Extendido)
El desarrollo se enfoca en un Producto Mínimo Viable (MVP) funcional, priorizando la arquitectura limpia, el aislamiento multi-tenant y la integración eficiente de servicios de IA:
* **Backend (ASP.NET Core):** API RESTful multi-tenant con aislamiento por `TenantId`, endpoints de autenticación JWT y comunicación en tiempo real (SignalR o WebSockets) para notificaciones de tickets críticos.
* **Base de Datos (PostgreSQL + pgvector):** Esquema relacional estructurado con soporte vectorial para almacenamiento de embeddings e índices optimizados por empresa.
* **Módulo de IA (RAG y Triaje):** Pipeline modular para consulta de preguntas frecuentes previo al envío, y clasificación automática (Tipo, Prioridad, Sentimiento y Resumen) tras el envío del ticket.
* **Widget Web (JavaScript Vanilla):** Archivo `.js` estático que inyecta dinámicamente un botón flotante con interfaz conversacional en dos pasos (Chat RAG -> Formulario de Radicación).
* **Contenedores (Docker-Compose):** Orquestación integral de la API y PostgreSQL vectorizado ejecutables con un solo comando.

## 3. Requerimientos Generales
* **Despliegue Unificado:** Todo el sistema (Base de datos y API) debe levantarse localmente ejecutando `docker-compose up`.
* **Arquitectura Limpia:** Organización por capas (Domain, Application, Infrastructure, API) aplicando principios SOLID y separación de responsabilidades.
* **Manejo Dinámico de CORS:** La API debe autorizar peticiones provenientes de los dominios externos configurados en cada Tenant.
* **Aislamiento Multi-tenant:** Inclusión y validación obligatoria del `TenantId` en encabezados HTTP (para peticiones públicas del widget) o en Claims JWT (para agentes).
* **Documentación Técnica:** Archivo `README.md` detallando la puesta en marcha, variables de entorno y guías para incrustar el widget.

## 4. Requerimientos Específicos

### A. Base de Datos (PostgreSQL + pgvector)
**Entidades Relacionales Mínimas**
* **Tenants:** Datos de las empresas suscriptoras, dominio permitido y API Key/Token del widget.
* **Users:** Agentes o administradores asociados a cada `TenantId`.
* **KnowledgeBaseArticles:** Preguntas frecuentes y artículos con columna de vector (`vector(1536)` o equivalente) para búsqueda RAG.
* **Tickets:** PQRS asociadas a un `TenantId`, con campos para cliente (nombre/correo), asunto, descripción, tipo (P/Q/R/S), estado (Pendiente, En Proceso, Resuelto), prioridad (Baja, Media, Alta), sentimiento y bandera de resolución por RAG.

**Índices y Rendimiento**
* Índices B-Tree en columnas de búsqueda y filtrado frecuente: `(TenantId, Status)` y `(TenantId, Priority)`.
* Índice vectorial (HNSW o IVFFlat) sobre la columna de embeddings filtrado por `TenantId`.

### B. Backend (ASP.NET Core)
**Endpoints Públicos (Widget External)**
* `POST /api/v1/widget/rag-search`: Recibe la consulta del chat y valida si existe solución en la base de conocimiento del tenant.
* `POST /api/v1/widget/tickets`: Recibe el formulario de PQRS e inicia la persistencia con triaje de IA.

**Endpoints Protegidos (JWT para Agentes)**
* Autenticación de usuarios (`/api/v1/auth/login`).
* CRUD de artículos de conocimiento (`/api/v1/kb-articles`) con generación automática de embeddings al guardar.
* Gestión de PQRS (`/api/v1/tickets`) con capacidades de listar, filtrar por estado/prioridad y actualizar ciclo de vida.

### C. Módulo de Inteligencia Artificial

#### 1. Auto-atención y Desviación de Tickets con RAG (Pre-radicación)
* **Búsqueda Contextual por Empresa:** Al escribir una duda en el widget, la API genera el vector embedding de la consulta y busca coincidencias por similitud coseno en la tabla `KnowledgeBaseArticles` del `TenantId` activo.
* **Respuesta Asistida por IA:** Si la similitud supera un umbral mínimo, el LLM sintetiza una respuesta directa basada exclusivamente en los artículos recuperados.
* **Validación de Solución:** El widget presenta la respuesta al usuario con la pregunta: "¿Esta respuesta resolvió tu inquietud?".
* **Desviación de Ticket:** Si el usuario responde Sí, finaliza la interacción y se registra una métrica de ticket desviado (ahorro operativo) sin crear un registro en la tabla `Tickets`.
* **Escalamiento a Formulario:** Si el usuario responde No, la interfaz abre automáticamente el formulario de radicación de PQRS.

#### 2. Triaje y Clasificación Automática (Post-radicación)
Cuando el usuario completa el formulario formal, la IA analiza el texto (Asunto + Descripción) mediante un prompt estructurado para extraer:
* **Clasificación del Tipo:** Petición, Queja, Reclamo o Sugerencia.
* **Prioridad Sugerida:** Baja (consultas/sugerencias), Media (peticiones estándar) o Alta (reclamos severos o insatisfacción crítica).
* **Análisis de Sentimiento:** Positivo, Neutro o Negativo.
* **Resumen Ejecutivo:** Síntesis breve (1 a 2 oraciones) para revisión rápida del agente.

### D. Widget Web (JavaScript Vanilla)
* **Script Empaquetado:** Inyección en sitios externos mediante `<script src="https://cdn.tu-saas.com/pqrs-widget.js" data-tenant="ID_EMPRESA"></script>`.
* **Renderizado Dinámico:** Inyección autónoma del HTML/CSS en el DOM (vía Shadow DOM o clases prefijadas) para aislar estilos.
* **Flujo Conversacional de Dos Fases:**
    * **Fase Chat / RAG:** Pantalla conversacional para consultas rápidas con botones de confirmación (¿Resolvió tu duda? [Sí][No]).
    * **Fase Formulario:** Formulario interactivo que captura Nombre, Correo, Asunto y Descripción si el usuario decide radicar la solicitud.
* **Envío Asíncrono:** Comunicación mediante `fetch` hacia la API en ambas fases con manejo visual de estado (cargando, éxito con número de radicado, error).

## 5. Matriz Comparativa de Arquitectura

| Módulo | Implementación MVP Estándar | Implementación MVP Extendido (RAG + Triaje IA) |
| :--- | :--- | :--- |
| **Base de Datos** | PostgreSQL Relacional | PostgreSQL + Extension pgvector para soporte híbrido |
| **Atención Inicial** | Formulario directo de tickets | Asistente conversacional RAG con desviación previa de consultas |
| **Procesamiento de PQRS** | Asignación manual de tipo y prioridad | Triaje automático vía IA (Tipo, Prioridad, Sentimiento, Resumen) |
| **Notificaciones** | Consultas por recarga de pantalla | Eventos en tiempo real para tickets de prioridad alta o sentimiento negativo |
