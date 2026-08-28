# PQRS SaaS Multi-tenant con IA — Especificación Técnica (v3.0)

> **Versión:** 3.0  
> **Fecha:** 2026-08-28  
> **Estado:** Completo (backend + frontends + tiempo real)

---

## 1. Visión General de la Arquitectura

**Aislamiento físico por tenant:** Cada tenant tiene su propia base de datos PostgreSQL, creada automáticamente en el momento del registro. No existe ninguna columna `TenantId` en las tablas operativas — la separación la da la base de datos misma.

- **`PqrsControlDb`** — Base compartida. Guarda el catálogo de tenants, sus orígenes CORS y los super administradores.
- **Una base `pqrs_tenant_<slug>` por cada tenant** — Datos operativos de ese tenant exclusivamente (usuarios/agentes, base de conocimiento, tickets). Requiere `CREATE EXTENSION vector`.

**Backend:** ASP.NET Core (.NET 10), arquitectura por capas (Domain, Application, Infrastructure, API) con EF Core 10 y Npgsql/pgvector.

```
┌────────────────────────────┐
│   PqrsControlDb (catálogo) │
│   Tenants / Dominios /     │
│   SuperAdmins / Config     │
└────────────┬───────────────┘
             │ crea en runtime
     ┌───────┴────────┐
     │ pqrs_tenant_1  │  ← una BD por tenant
     │ pqrs_tenant_2  │
     └────────────────┘
```

---

## 2. Esquema de `PqrsControlDb`

### Tabla `Tenants`

| Columna | Tipo | Notas |
|---|---|---|
| Id | uuid (PK) | |
| Nombre | text | |
| Slug | text (unique) | Usado para nombrar la BD: `pqrs_tenant_<slug>` |
| DominioPermitido | text | Para CORS (backward-compat) |
| ApiKeyWidget | text (unique) | Token público del widget |
| NombreBaseDatos | text | `pqrs_tenant_<slug>` |
| EstadoProvisionamiento | enum (text) | Pendiente / Completado / Error |
| Activo | boolean | |
| FechaCreacion | timestamp | |

### Tabla `TenantDominios`

| Columna | Tipo | Notas |
|---|---|---|
| Id | uuid (PK) | |
| TenantId | uuid (FK → Tenants) | |
| Origen | text | Origen CORS permitido (un tenant puede tener varios) |

### Tabla `SuperAdmins`

| Columna | Tipo | Notas |
|---|---|---|
| Id | uuid (PK) | |
| Email | text | |
| PasswordHash | text | Hasheado |
| Activo | boolean | |
| FechaCreacion | timestamp | |

### Tabla `TenantConfiguraciones`

| Columna | Tipo | Notas |
|---|---|---|
| Id | uuid (PK) | |
| TenantId | uuid (FK → Tenants) | |
| ColorPrimario | text | |
| Logo | text (url) | |
| UmbralSimilitudRAG | float | Default 0.75 |
| LimiteTicketsMes | int | Opcional |

**Nota:** El connection string de cada tenant **no** se guarda como texto plano — se reconstruye en runtime a partir de `NombreBaseDatos` + una plantilla compartida (`TenantTemplate` con `{db}`) configurada por environment/secret manager.

---

## 3. Flujo de Aprovisionamiento (Al Registrar un Tenant)

`POST /api/v1/tenants/registro`:

1. Insertar el tenant en `PqrsControlDb` con `EstadoProvisionamiento = Pendiente` y el `Slug`/`NombreBaseDatos` calculado.
2. Abrir una conexión ADO.NET a la base de mantenimiento de Postgres (`postgres`) usando una connection string **administrativa** (`TenantAdmin`, requiere permiso `CREATEDB`) y ejecutar `CREATE DATABASE pqrs_tenant_<slug>`.
3. Construir el connection string de la nueva base (mismo host/usuario/password, cambia solo el nombre de la BD).
4. Instanciar un `CoreDbContext` apuntando a esa connection string y ejecutar `context.Database.Migrate()` — aplica las migraciones Core: tablas `Users`, `KnowledgeBaseArticles`, `Tickets`, y la habilitación de `CREATE EXTENSION IF NOT EXISTS vector`.
5. Si todo sale bien: `EstadoProvisionamiento = Completado`. Si falla: marcar `EstadoProvisionamiento = Error` (la BD queda creada; limpieza pendiente como TODO) y devolver 500 con detalle.
6. **Sembrar el usuario Administrador inicial:** se crea un usuario con rol `Administrador` y contraseña temporal. Si hay SMTP configurado, se envía por correo (con el `Slug` y el enlace al panel); en desarrollo sin SMTP se devuelve en la respuesta **una sola vez**.

---

## 4. Resolución de Tenant por Request

- Middleware `TenantResolutionMiddleware` resuelve el tenant de dos formas (sin reordenar el pipeline):
  1. **Widget (no autenticado):** header `X-Tenant-Api-Key` → busca por `ApiKeyWidget`.
  2. **Agentes autenticados:** claim `tenantId` del JWT.
- Obtiene `NombreBaseDatos` → reconstruye el connection string → lo guarda en `context.Items["TenantId"]` y en el servicio *scoped* `ITenantConnectionAccessor`.
- `CoreDbContext` lee el connection string desde `ITenantConnectionAccessor` en `OnConfiguring` — cada request opera automáticamente contra la base correcta, sin que ningún query filtre por `TenantId`. Si el accessor está vacío, lanza (así fallan los requests sin tenant).
- Orden del pipeline (crítico): `UseAuthentication` → `TenantResolutionMiddleware` → `UseAuthorization`.
- **Excepción:** `AuthController.Login` setea el accessor manualmente desde `tenantSlug` del body (antes de existir token).
- `JwtSecurityTokenHandler.DefaultMapInboundClaims = false` mantiene los claims con sus tipos emitidos (`sub`, `role`, `tenantId`, `tenantSlug`).

---

## 5. Esquema de cada Base `pqrs_tenant_<slug>` (requiere `CREATE EXTENSION vector`)

### Tabla `Users`

| Columna | Tipo | Notas |
|---|---|---|
| Id | uuid (PK) | |
| Email | text | |
| PasswordHash | text | |
| Rol | enum (text) | Agente / Administrador |
| Activo | boolean (default true) | |
| DebeCambiarPassword | boolean | Marca credenciales temporales; fuerza el cambio en el primer login |
| FechaCreacion | timestamp | |

### Tabla `KnowledgeBaseArticles`

| Columna | Tipo | Notas |
|---|---|---|
| Id | uuid (PK) | |
| Pregunta | text | Para importaciones, un label corto del fragmento |
| Respuesta | text | |
| Titulo | text (nullable) | Nombre del documento fuente cuando viene de ingesta automática |
| Embedding | vector(768) | `gemini-embedding-001`, `output_dimensionality=768` |
| FechaCreacion | timestamp | |
| FechaActualizacion | timestamp | |

**Índice HNSW sobre `Embedding`** con `vector_cosine_ops` explícito (pgvector ≥ 0.8 no tiene operator class por defecto; sin él falla con `42704`).

### Tabla `Tickets`

| Columna | Tipo | Notas |
|---|---|---|
| Id | uuid (PK) | |
| NumeroRadicado | text | Generado: `PQRS-YYYY-NNNN` (conteo del año + 1) |
| ClienteNombre | text | |
| ClienteCorreo | text | |
| Asunto | text | |
| Descripcion | text | |
| Tipo | enum (text) | Peticion / Queja / Reclamo / Sugerencia (asignado por IA) |
| Estado | enum (text) | Pendiente / EnProceso / Resuelto |
| Prioridad | enum (text) | Baja / Media / Alta (asignado por IA) |
| Sentimiento | enum (text) | Positivo / Neutro / Negativo (asignado por IA) |
| Resumen | text | Asignado por IA |
| ResueltoPorRAG | boolean (default false) | |
| FechaCreacion | timestamp | |
| FechaActualizacion | timestamp | |

Los enums se serializan como **strings** vía `JsonStringEnumConverter` global.

---

## 6. Módulos del Backend (Estado Final)

| # | Módulo | Estado |
| :--- | :--- | :--- |
| 1 | Infraestructura — docker-compose (Postgres + pgvector, API) | ✅ Completado |
| 2 | Aprovisionamiento Dinámico de Tenants | ✅ Completado |
| 3 | Resolución de Tenant por Request (Middleware + `ITenantConnectionAccessor`) | ✅ Completado |
| 4 | Auth JWT (agentes + superadmin + bootstrap) | ✅ Completado |
| 5 | CRUD de `KnowledgeBaseArticles` + importación de documentos | ✅ Completado |
| 6 | CRUD de `Tickets` para Agentes | ✅ Completado |
| 7 | Módulo IA — RAG (Gemini) | ✅ Completado |
| 8 | Módulo IA — Triaje (Gemini) | ✅ Completado |
| 9 | CORS dinámico multi-origen por tenant | ✅ Completado |
| 10 | Envío de credenciales por SMTP | ✅ Completado |
| 11 | Dashboard del Super Administrador | ✅ Completado |
| 12 | Dashboard de Agentes/Dueños del Tenant | ✅ Completado |
| 13 | Widget JS (Vanilla) | ✅ Completado |
| 14 | **Tiempo Real (SignalR)** | ✅ Completado |

---

## 7. Endpoints de la API (Resumen)

### Endpoints Públicos (Widget) — header `X-Tenant-Api-Key`

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| POST | `/api/v1/tenants/registro` | Registra un tenant, aprovisiona su BD y siembra el admin |
| POST | `/api/v1/auth/login` | Login de agentes/administradores del tenant |
| POST | `/api/v1/widget/rag-search` | Búsqueda RAG en la base de conocimiento |
| POST | `/api/v1/widget/tickets` | Radicación de PQRS con triaje de IA |

### Endpoints Protegidos (JWT para Agentes)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| GET | `/api/v1/agents/yo` | Información del agente autenticado |
| GET | `/api/v1/agents` | Lista de agentes (solo Administradores) |
| PUT | `/api/v1/agents/me/password` | Cambiar la propia contraseña |
| POST | `/api/v1/agents/invite` | Invitar a un agente (solo Administradores) |
| PUT | `/api/v1/agents/{id}/desactivar` | Desactivar un agente (legacy) |
| PUT | `/api/v1/agents/{id}/estado` | Activar/desactivar (body `{ activo }`, solo Administradores) |
| GET | `/api/v1/kb-articles` | Lista de artículos |
| POST | `/api/v1/kb-articles` | Crear artículo (genera embedding) |
| POST | `/api/v1/kb-articles/import` | Importar TXT/MD/PDF/DOCX (chunking + embeddings) |
| PUT | `/api/v1/kb-articles/{id}` | Editar artículo (regenera embedding) |
| DELETE | `/api/v1/kb-articles/{id}` | Eliminar artículo |
| GET | `/api/v1/tickets?estado=&prioridad=` | Lista de tickets con filtros |
| GET | `/api/v1/tickets/{id}` | Detalle de un ticket |
| PUT | `/api/v1/tickets/{id}/estado` | Cambiar estado |
| PUT | `/api/v1/tickets/{id}/prioridad` | Cambiar prioridad |

### Endpoints del Super Administrador — rol `SuperAdmin`

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| POST | `/api/v1/auth/login-superadmin` | Login (bootstrap del primero si no existe) |
| POST | `/api/v1/superadmins` | Registrar otro superadmin |
| GET | `/api/v1/superadmins` | Listar superadmins |
| PUT | `/api/v1/superadmins/me/password` | Cambiar la propia contraseña |
| GET | `/api/v1/tenants` | Listar tenants (con `dominios[]`) |
| PUT | `/api/v1/tenants/{id}/estado` | Activar/desactivar un tenant |

### Tiempo Real (SignalR)

| Recurso | Descripción |
| :--- | :--- |
| `GET`/`WS` `/hubs/tickets` | Hub para agentes autenticados. Agrupa por `tenant-<tenantId>` |
| Evento `TicketNuevo` | Se emite al radicar un ticket desde el widget |
| Evento `TicketActualizado` | Se emite al cambiar estado/prioridad |

---

## 8. Frontends

Aplicaciones estáticas (JavaScript vanilla, sin build) servidas por **nginx** (`frontends/nginx.conf` + `frontends/web.Dockerfile`) en el puerto 8080.

| Ruta | Contenido |
| :--- | :--- |
| `/superadmin/` | Dashboard del super administrador |
| `/agent/` | Dashboard de agentes (login por tenant, roles) |
| `/widget/pqrs-widget.js` | Widget JS incrustable (IIFE + Shadow DOM) |
| `/widget/demo.html` | Demo del widget |
| `/api/v1/*` | Proxy a la API |
| `/hubs/tickets` | Proxy WebSocket/SignalR |
| `/` | Landing |

**Autenticación:**
- **Superadmin:** token en cookie `sa_token`; se envía como `Authorization: Bearer`.
- **Agentes:** login `slug + email + contraseña`; cookies `ag_token` / `ag_rol` / `ag_slug`. Si `debeCambiarPassword` es true, se fuerza el cambio en el primer login.
- Los dashboards llaman a la API por `/api/v1` relativo (same-origin a través de nginx), por lo que **no requieren CORS**. El widget (embebido en sitios de clientes) usa el CORS multi-origen por tenant.
- **SeñalR:** `index.html` carga `@microsoft/signalr@9.0.19` por CDN; `config.js` exporta `HUB_URL` (`http(s)://.../hubs/tickets`); `js/realtime.js` conecta con `accessTokenFactory` + `withAutomaticReconnect`, muestra toast y recarga tickets en `TicketNuevo`/`TicketActualizado`. El JWT viaja por `?access_token=` (los navegadores no pueden poner `Authorization` en el handshake WS).

**CORS:** dinámico por tenant (`TenantCorsPolicyProvider`): permite un origen si coincide con algún `TenantDominios` de un tenant activo **o** está en la lista global `Cors:AllowedOrigins`.

---

## 9. Decisiones de Alcance (Estado Final)

| Decisión | Estado |
| :--- | :--- |
| Plan B (pool de bases de datos) | No implementado; se mantiene como mejora futura |
| **Tiempo Real (SignalR)** | **Implementado** (tickets + cambios de estado para agentes) |
| Un solo dashboard para agentes y administradores | Implementado (roles muestran/ocultan secciones) |
| Contraseña del administrador mostrada/envida una sola vez | Implementado; el flujo de invitación la refuerza en v2 |
| Notificación al usuario final del estado de su PQRS | Mejora futura (solo los agentes reciben tiempo real hoy) |

---

## 10. Verificación

- No hay framework de tests ni CI en el repo.
- Verificación principal: `dotnet build` + `docker compose up --build`.
- La base de **control** no se migra sola: aplicar una vez `dotnet ef database update -c ControlDbContext -p src/PqrsSaas.Infrastructure -s src/PqrsSaas.Api`.
- Las bases por tenant se migran solas al registrarse.

---

## 11. Documentación y Entregables Finales

- ✅ Código fuente completo en GitHub.
- ✅ `README.md` con instalación, variables de entorno e incrustación del widget.
- ✅ `.env.example` + `appsettings.Development.json` con placeholders.
- ✅ `docker-compose.yml` funcional.
- ✅ `AGENTS.md` (guía técnica) + `docs/estructura.md` + `docs/proyecto.md`.
- ⏳ Mejoras futuras (TODO): pool de bases, paginación, notificaciones al cliente final, limpieza de BDs huérfanas en aprovisionamiento fallido.

---

**Fin del Documento**
