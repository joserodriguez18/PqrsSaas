# PQRS SaaS Multi-tenant con IA — Especificación Técnica para Desarrollo (v2.0)

> **Versión:** 2.0  
> **Fecha:** 2026-08-27  
> **Estado:** En Desarrollo (Priorización para Entrega)  
> **Presupuesto de Tiempo:** 6 horas (para el código base) + tiempo para frontends

---

## 1. Visión General de la Arquitectura

**Aislamiento físico por tenant:** Cada tenant tiene su propia base de datos PostgreSQL, creada automáticamente en el momento del registro. No existe ninguna columna `TenantId` en las tablas operativas — la separación la da la base de datos misma.

- **`PqrsControlDb`** — Base compartida. Guarda el catálogo de tenants y cómo conectarse a la base de datos de cada uno.
- **Una base `pqrs_tenant_<slug>` por cada tenant** — Datos operativos de ese tenant exclusivamente (usuarios/agentes, base de conocimiento, tickets). Requiere `CREATE EXTENSION vector`.

**Backend:** ASP.NET Core, arquitectura por capas (Domain, Application, Infrastructure, API).

---

## 2. Esquema de `PqrsControlDb`

### Tabla `Tenants`

| Columna | Tipo | Notas |
|---|---|---|
| Id | uuid (PK) | |
| Nombre | text | |
| Slug | text (unique) | Usado para nombrar la BD: `pqrs_tenant_<slug>` |
| DominioPermitido | text | Para CORS |
| ApiKeyWidget | text (unique) | Token público del widget |
| NombreBaseDatos | text | `pqrs_tenant_<slug>` |
| EstadoProvisionamiento | text | Pendiente / Completado / Error |
| Activo | boolean | |
| FechaCreacion | timestamp | |

### Tabla `TenantConfiguraciones`

| Columna | Tipo | Notas |
|---|---|---|
| Id | uuid (PK) | |
| TenantId | uuid (FK → Tenants) | Aquí sí aplica FK real: misma base |
| ColorPrimario | text | |
| Logo | text (url) | |
| UmbralSimilitudRAG | float | Default ~0.75 |
| LimiteTicketsMes | int | Opcional |

**Nota:** El connection string de cada tenant **no** se guarda como texto plano en la tabla — se reconstruye en runtime a partir de `NombreBaseDatos` + una plantilla de host/usuario/password compartida (en configuración/secret manager).

---

## 3. Flujo de Aprovisionamiento (Al Registrar un Tenant)

`POST /api/v1/tenants/registro`:

1. Insertar el tenant en `PqrsControlDb` con `EstadoProvisionamiento = Pendiente` y el `Slug`/`NombreBaseDatos` calculado.
2. Abrir una conexión ADO.NET a la base de mantenimiento de Postgres (`postgres`) usando una connection string **administrativa** (con permiso `CREATEDB`) y ejecutar `CREATE DATABASE pqrs_tenant_<slug>`.
3. Construir el connection string de la nueva base (mismo host/usuario/password, cambia solo el nombre de la BD).
4. Instanciar un `CoreDbContext` apuntando a esa connection string y ejecutar `context.Database.Migrate()` — aplica todas las migraciones EF Core: tablas `Users`, `KnowledgeBaseArticles`, `Tickets`, y una migración inicial con `migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector")`.
5. Si todo sale bien: `EstadoProvisionamiento = Completado`. Si falla: capturar el error, marcar `EstadoProvisionamiento = Error` (y opcionalmente hacer `DROP DATABASE` de limpieza), devolver 500 con detalle.
6. **Sembrar el usuario Administrador inicial:** Al completar el registro, se crea un usuario con rol `Administrador` en la base del tenant, con una contraseña generada aleatoriamente. La respuesta incluye esta contraseña **una sola vez**.

**Decisión de Alcance:** El Plan B (pool de bases de datos) está documentado pero **no se implementará en esta versión** por tiempo. Se mantiene como mejora futura.

---

## 4. Resolución de Tenant por Request

- Middleware `TenantResolutionMiddleware`: lee `ApiKey`/`TenantId` del header (widget) o claim del JWT (agentes autenticados) → busca en `PqrsControlDb.Tenants` → obtiene `NombreBaseDatos` → reconstruye el connection string.
- Ese connection string se guarda en un servicio *scoped* (`ITenantConnectionAccessor`) por request.
- `CoreDbContext` se configura para leer el connection string desde `ITenantConnectionAccessor` en `OnConfiguring` — así cada request opera automáticamente contra la base correcta, sin que ningún query necesite filtrar por `TenantId`.

---

## 5. Esquema de cada Base `pqrs_tenant_<slug>` (requiere `CREATE EXTENSION vector`)

### Tabla `Users`

| Columna | Tipo |
|---|---|
| Id | uuid (PK) |
| Email | text |
| PasswordHash | text |
| Rol | text (Agente / Administrador) |
| Activo | boolean (default true) |
| FechaCreacion | timestamp |

### Tabla `KnowledgeBaseArticles`

| Columna | Tipo |
|---|---|
| Id | uuid (PK) |
| Pregunta | text |
| Respuesta | text |
| Embedding | vector(768) — generado con `gemini-embedding-001`, `output_dimensionality=768` |
| FechaCreacion | timestamp |
| FechaActualizacion | timestamp |

**Índice HNSW sobre `Embedding`** — ya no necesita filtrar por tenant porque la base entera es de un solo tenant.

### Tabla `Tickets`

| Columna | Tipo |
|---|---|
| Id | uuid (PK) |
| NumeroRadicado | text (generado automáticamente, ej: PQRS-2026-0001) |
| ClienteNombre | text |
| ClienteCorreo | text |
| Asunto | text |
| Descripcion | text |
| Tipo | text (asignado por IA) |
| Estado | text (Pendiente, En Proceso, Resuelto) |
| Prioridad | text (asignado por IA: Baja, Media, Alta) |
| Sentimiento | text (asignado por IA: Positivo, Neutro, Negativo) |
| Resumen | text (asignado por IA) |
| ResueltoPorRAG | boolean (default false) |
| FechaCreacion | timestamp |
| FechaActualizacion | timestamp |

**Índices B-Tree en `(Estado)` y `(Prioridad)`** — ya no compuestos con TenantId.

---

## 6. Módulos del Backend (Orden de Construcción Priorizado)

| Orden | Módulo | Estado | Prioridad |
| :--- | :--- | :--- | :--- |
| 1 | **Infraestructura** — docker-compose con Postgres (+ pgvector), API .NET | ✅ Completado | - |
| 2 | **Aprovisionamiento Dinámico de Tenants** | ✅ Completado | - |
| 3 | **Resolución de Tenant por Request** (Middleware + `ITenantConnectionAccessor`) | ✅ Completado | - |
| 4 | **Auth JWT** — login, admin sembrado | ✅ Completado | - |
| 5 | **CRUD de `KnowledgeBaseArticles`** | ❌ Por hacer | 🔴 Alta |
| 6 | **CRUD de `Tickets` para Agentes** | ❌ Por hacer | 🔴 Alta |
| 7 | **Módulo IA — RAG (Gemini)** — `POST /widget/rag-search` | ❌ Por hacer | 🔴 Alta |
| 8 | **Módulo IA — Triaje (Gemini)** — `POST /widget/tickets` | ❌ Por hacer | 🔴 Alta |
| 9 | **Dashboard del Super Administrador** | ❌ Por hacer | 🟡 Media |
| 10 | **Dashboard de Agentes/Dueños del Tenant** | ❌ Por hacer | 🔴 Alta |
| 11 | **Widget JS (Vanilla)** | ❌ Por hacer | 🔴 Alta |
| 12 | **Tiempo Real (SignalR)** | ❌ Por hacer | 🟢 Baja (postergado) |

---

## 7. Endpoints de la API (Resumen)

### Endpoints Públicos (Widget)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| POST | `/api/v1/widget/rag-search` | Búsqueda RAG en la base de conocimiento del tenant |
| POST | `/api/v1/widget/tickets` | Radicación de PQRS con triaje de IA |

### Endpoints Protegidos (JWT para Agentes)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| POST | `/api/v1/auth/login` | Autenticación de agentes/administradores |
| GET | `/api/v1/tickets` | Lista de tickets (con filtros por estado/prioridad) |
| GET | `/api/v1/tickets/{id}` | Detalle de un ticket |
| PUT | `/api/v1/tickets/{id}/estado` | Cambiar estado del ticket |
| PUT | `/api/v1/tickets/{id}/prioridad` | Cambiar prioridad del ticket |
| GET | `/api/v1/kb-articles` | Lista de artículos de la base de conocimiento |
| POST | `/api/v1/kb-articles` | Crear un artículo |
| PUT | `/api/v1/kb-articles/{id}` | Editar un artículo |
| DELETE | `/api/v1/kb-articles/{id}` | Eliminar un artículo |
| POST | `/api/v1/agents/invite` | Invitar a un nuevo agente (solo Administradores) |
| PUT | `/api/v1/agents/{id}/desactivar` | Desactivar un agente (solo Administradores) |

### Endpoints del Super Administrador

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| POST | `/api/v1/auth/login-superadmin` | Login para el super administrador |
| GET | `/api/v1/tenants` | Lista de todos los tenants |
| PUT | `/api/v1/tenants/{id}/estado` | Activar/desactivar un tenant |

---

## 8. Frontends (Generados con Stitch)

Los frontends se construirán usando los siguientes prompts, que ya están diseñados para conectarse a la API existente.

### A. Dashboard del Super Administrador (Prompt)

> **Contexto:** Este dashboard es solo para el dueño del SaaS. Debe ser simple y funcional.
>
> **Ver Prompt Completo:** Sección "Prompt 1" del documento de diseño.

### B. Dashboard de Agentes/Dueños del Tenant (Prompt)

> **Contexto:** Este es el producto principal. Los dueños de los tenants lo usan para gestionar su soporte.
>
> **Ver Prompt Completo:** Sección "Prompt 2" del documento de diseño.

### C. Widget Web (JavaScript Vanilla) (Prompt)

> **Contexto:** Este es el script que los clientes incrustarán en sus sitios web.
>
> **Ver Prompt Completo:** Sección "Prompt 3" del documento de diseño.

---

## 9. Decisiones de Alcance para la Entrega

| Decisión | Justificación |
| :--- | :--- |
| **No implementar el Plan B (pool de bases de datos)** | El aprovisionamiento dinámico actual funciona y cumple con el requisito de aislamiento. Refactorizar ahora es un riesgo innecesario. |
| **No construir SignalR** | La notificación en tiempo real es "nice to have". Se documenta como mejora futura. |
| **Un solo dashboard para agentes y administradores** | Un dashboard con roles (mostrar/ocultar secciones) reduce drásticamente el tiempo de desarrollo. |
| **Priorizar el widget sobre el dashboard del super admin** | El widget es la interfaz del usuario final. Sin él, el producto no tiene sentido. |
| **La contraseña del administrador se muestra una sola vez** | Aceptado para el MVP. Se mejora con un flujo de invitación en la v2. |

---

## 10. Plan de Entrega (Cronograma)

| Fase | Días | Actividades Clave | Entregable |
| :--- | :--- | :--- | :--- |
| **1** | Día 1-2 | Terminar endpoints del backend (CRUD de KB, CRUD de tickets, endpoints del widget) | API completa y probada |
| **2** | Día 3-4 | Generar y adaptar Dashboard de Agentes (usando Stitch). Integrar con la API. | Dashboard funcional |
| **3** | Día 5-6 | Generar y adaptar Widget (usando Stitch). Probar el flujo completo (RAG → formulario). | Widget funcional |
| **4** | Día 7 | Generar Dashboard del Super Administrador (si queda tiempo). | Dashboard de admin (opcional) |
| **5** | Día 7 | Documentación final (README actualizado). | Proyecto listo para entregar |

---

## 11. Documentación y Entregables Finales

- ✅ Código fuente completo en GitHub.
- ✅ `README.md` actualizado con instrucciones de instalación, variables de entorno, y cómo incrustar el widget.
- ✅ `docker-compose.yml` funcional.
- ✅ Documentación de la API (Swagger/Postman).
- ✅ Lista de "Mejoras Futuras" (TODO.md) con: flujo de invitación, pool de bases de datos, SignalR, paginación.

---

## 12. Notas Adicionales para el Desarrollador

1.  **La integración con Gemini es crítica:** Prueba los endpoints de RAG y Triaje **antes** de empezar con los frontends. Asegúrate de que las claves de API funcionan y los embeddings se generan correctamente.
2.  **El widget debe ser probado en un entorno real:** Sirve el widget desde tu API y pruébalo en un sitio HTML simple (fuera de tu dominio) para validar CORS y el funcionamiento del Shadow DOM.
3.  **No te obsesiones con el diseño:** Un diseño limpio y funcional es suficiente para el MVP. El cliente valora más que funcione.
4.  **Documenta mientras avanzas:** No dejes la documentación para el final. Actualiza el README a medida que completas módulos.

---

**Fin del Documento**