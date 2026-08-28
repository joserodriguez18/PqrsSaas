# PQRS SaaS — Plataforma Multi-tenant de Gestión de PQRS con IA

SaaS para centralizar y automatizar la gestión de Peticiones, Quejas, Reclamos y Sugerencias
(PQRS) de pequeñas y medianas empresas. Incluye un widget web incrustable que ofrece
auto-atención vía **RAG** (sobre la base de conocimiento de la empresa) y, si el usuario lo
necesita, un formulario formal de radicación con **triaje automático de IA** (tipo, prioridad,
sentimiento y resumen).

**Stack:** ASP.NET Core (.NET 10) · EF Core 10 · PostgreSQL + pgvector · Google Gemini.

---

## Arquitectura (dos niveles de bases de datos)

- **`PqrsControlDb`** — base compartida (catálogo). Tablas: `Tenants`, `TenantConfiguraciones`, `SuperAdmins`.
- **Una base `pqrs_tenant_<slug>` por cada tenant** — creada automáticamente al registrarse.
  Tablas: `Users`, `KnowledgeBaseArticles`, `Tickets`. El aislamiento lo da la propia base de datos
  (no hay columnas `TenantId`).

Cada request resuelve su conexión: el widget mediante el header `X-Tenant-Api-Key`, los agentes
mediante el claim `tenantId` del JWT. La separación física garantiza que ningún query cruce datos
entre empresas.

```
┌────────────────────────────┐
│   PqrsControlDb (catálogo) │
│   Tenants / SuperAdmins    │
└────────────┬───────────────┘
             │ crea en runtime
     ┌───────┴────────┐
     │ pqrs_tenant_1  │  ← una BD por tenant
     │ pqrs_tenant_2  │
     └────────────────┘
```

---

## Requisitos

- [Docker](https://www.docker.com/products/docker-desktop/) (con Docker Compose).
- [.NET SDK 10](https://dotnet.microsoft.com/) (solo para desarrollo/migraciones).
- Una API key de [Google Gemini](https://ai.google.dev/) con acceso a:
  - `gemini-embedding-001` (embeddings de 768 dimensiones).
  - `gemini-3.5-flash-lite` (generación de texto / triaje).

---

## Puesta en marcha

### 1. Crear el archivo `.env`

Copia el ejemplo y completa los valores:

```bash
# .env  (está en .gitignore, no se versiona)
JWT_SECRET=una_cadena_aleatoria_de_al_menos_32_caracteres
GEMINI_API_KEY=tu_api_key_de_gemini
# Opcionales:
# SUPERADMIN_EMAIL=superadmin@pqrs.local
# SUPERADMIN_PASSWORD=admin123
# SMTP_HOST=smtp.tu-proveedor.com
# SMTP_PORT=587
# SMTP_USER=tu_usuario
# SMTP_PASSWORD=tu_clave
# SMTP_FROM=no-reply@pqrs.local
# PANEL_BASE_URL=http://localhost:8080   # enlace al panel que va en los correos de credenciales
```

> `JWT_SECRET` se usa para firmar los tokens. Genérala con `openssl rand -base64 48`.

### 2. Levantar el stack

```bash
docker compose up --build
```

Esto arranca:
- **Postgres** (`pgvector/pgvector:pg16`) en el puerto `5432`.
- **API** en `http://localhost:5000` (Swagger en `/swagger`).
- **Web (nginx)** en `http://localhost:8080` — punto de entrada por rutas:
  - `http://localhost:8080/superadmin/` → dashboard de super administrador.
  - `http://localhost:8080/agent/` → dashboard de agentes (por tenant).
  - `http://localhost:8080/widget/pqrs-widget.js` → widget JS (incrustable).
  - `http://localhost:8080/widget/demo.html` → demo del widget.
  - `http://localhost:8080/api/v1/*` → API (proxy).
  - `http://localhost:8080/` → landing.

### 3. Migrar la base de control (una sola vez)

La base de **control** **no** se migra automáticamente. Ejecuta una vez:

```bash
dotnet ef database update -c ControlDbContext -p src/PqrsSaas.Infrastructure -s src/PqrsSaas.Api
```

> Las bases por **tenant** se migran solas al registrarse, no requieren este paso.

---

## Flujo de uso (ejemplo completo)

### 1. Registrar un tenant (crea su BD + siembra el administrador)

```bash
curl -X POST http://localhost:5000/api/v1/tenants/registro \
  -H "Content-Type: application/json" \
  -d '{"nombre":"Empresa Demo","dominioPermitido":"https://demo.com","emailAdministrador":"admin@demo.com"}'
```

La respuesta incluye el `ApiKeyWidget` y la **contraseña del administrador, que solo se muestra
una vez** (no se puede recuperar después):

```json
{
  "id": "2c9aefeb-...",
  "slug": "empresa_demo",
  "apiKeyWidget": "3c61bfdd...",
  "credencialesAdmin": { "emailAdministrador": "admin@demo.com", "password": "57af54fa6aee" }
}
```

### 2. Login del administrador del tenant

```bash
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantSlug":"empresa_demo","email":"admin@demo.com","password":"57af54fa6aee"}'
```

Devuelve un JWT. Se usa como `Authorization: Bearer <token>` en los endpoints protegidos.

### 3. Cargar la base de conocimiento (genera embeddings)

```bash
curl -X POST http://localhost:5000/api/v1/kb-articles \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"pregunta":"¿Cómo cambio mi contraseña?","respuesta":"Desde la sección de perfil de tu cuenta."}'
```

### 4. Importar documentación completa (chunking automático)

En lugar de crear artículos uno por uno, puedes subir un documento (`.txt`, `.md`, `.pdf`, `.docx`)
y la API lo trocea en fragmentos y genera un embedding por fragmento:

```bash
curl -X POST http://localhost:5000/api/v1/kb-articles/import \
  -H "Authorization: Bearer <token>" \
  -F "archivo=@politicas.pdf"
```

> Respuesta: `{ "mensaje": "...", "archivo": "politicas.pdf", "fragmentos": <n> }`. Límite de 5 MB
> y hasta 150 fragmentos por documento.

### 5. Widget: auto-atención por RAG

```bash
curl -X POST http://localhost:5000/api/v1/widget/rag-search \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Api-Key: 3c61bfdd..." \
  -d '{"consulta":"cómo cambio mi contraseña"}'
```

Responde `{ "encontrado": true|false, "saludo": true|false, "respuesta": "...", "sintetizada": true|false, "coincidencias": [...] }`. Los saludos ("hola", "buenos días") devuelven una respuesta de bienvenida fija sin usar IA. Si el usuario
indica que su duda **no** se resolvió, el widget abre el formulario de radicación. (Si Gemini está saturado,
la respuesta se devuelve con `sintetizada: false` usando el texto del artículo más relevante.)

### 6. Widget: radicar una PQRS (triaje IA)

```bash
curl -X POST http://localhost:5000/api/v1/widget/tickets \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Api-Key: 3c61bfdd..." \
  -d '{"clienteNombre":"Juan Pérez","clienteCorreo":"juan@mail.com","asunto":"No me devuelven mi dinero","descripcion":"..."}'
```

Devuelve el radicado (`PQRS-2026-0001`) y la clasificación automática: tipo, prioridad,
sentimiento y resumen.

### 7. Login del super administrador (primer acceso = bootstrap)

```bash
curl -X POST http://localhost:5000/api/v1/auth/login-superadmin \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@pqrs.local","password":"admin123"}'
```

> Si no existe ningún superadmin, este primer login siembra el registro a partir de
> `SUPERADMIN_EMAIL` / `SUPERADMIN_PASSWORD`. Después, los superadmins se gestionan por la API.

---

## Incrustar el widget

Cada tenant tiene un `ApiKeyWidget`. Para incrustar el asistente en el sitio del cliente:

```html
<script src="https://TU-DOMINIO/widget/pqrs-widget.js"
    data-tenant="<ApiKeyWidget>"
    data-api-url="https://TU-API/api/v1"></script>
```

Atributos opcionales: `data-color` (color primario, por defecto `#3525cd`), `data-title` (título, por defecto "Asistente Virtual").

**Flujo:** chat RAG → respuesta → "¿Resolvió tu duda? [Sí/No]" → si "No", formulario de radicación → número de radicado.

> El widget corre en el dominio del cliente y llama a la API con `X-Tenant-Api-Key`. Para que funcione, el `DominioPermitido` del tenant debe coincidir **exactamente** con el origen del sitio (esquema+host+puerto). CORS dinámico multi-origen por tenant.

## Referencia de la API

### Públicos (widget) — header `X-Tenant-Api-Key`

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `POST` | `/api/v1/tenants/registro` | Registra un tenant, aprovisiona su BD y siembra el admin. |
| `POST` | `/api/v1/auth/login` | Login de agentes/administradores del tenant. |
| `POST` | `/api/v1/widget/rag-search` | Búsqueda RAG en la base de conocimiento del tenant. |
| `POST` | `/api/v1/widget/tickets` | Radicación formal de PQRS con triaje de IA. |

### Protegidos (agentes) — `Authorization: Bearer <token>`

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/v1/agents/yo` | Información del agente autenticado. |
| `GET` | `/api/v1/agents` | Lista de agentes (solo Administradores). |
| `PUT` | `/api/v1/agents/me/password` | Cambiar la propia contraseña. |
| `POST` | `/api/v1/agents/invite` | Invitar a un agente (solo Administradores). |
| `PUT` | `/api/v1/agents/{id}/estado` | Activar/desactivar un agente (solo Administradores). |
| `GET` | `/api/v1/kb-articles` | Listar artículos de la base de conocimiento. |
| `POST` | `/api/v1/kb-articles` | Crear artículo (genera embedding). |
| `POST` | `/api/v1/kb-articles/import` | Importar un documento TXT/MD/PDF/DOCX (lo trocea en chunks y genera embeddings). |
| `PUT` | `/api/v1/kb-articles/{id}` | Editar un artículo (regenera embedding). |
| `DELETE` | `/api/v1/kb-articles/{id}` | Eliminar un artículo. |
| `GET` | `/api/v1/tickets?estado=&prioridad=` | Listar tickets con filtros opcionales. |
| `GET` | `/api/v1/tickets/{id}` | Detalle de un ticket. |
| `PUT` | `/api/v1/tickets/{id}/estado` | Cambiar estado (Pendiente/EnProceso/Resuelto). |
| `PUT` | `/api/v1/tickets/{id}/prioridad` | Cambiar prioridad (Baja/Media/Alta). |

### Super administrador — rol `SuperAdmin`

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `POST` | `/api/v1/auth/login-superadmin` | Login (bootstrap del primero si no existe ninguno). |
| `POST` | `/api/v1/superadmins` | Registrar otro superadmin. |
| `GET` | `/api/v1/superadmins` | Listar superadmins. |
| `PUT` | `/api/v1/superadmins/me/password` | Cambiar la propia contraseña. |
| `GET` | `/api/v1/tenants` | Listar todos los tenants. |
| `PUT` | `/api/v1/tenants/{id}/estado` | Activar/desactivar un tenant. |

> **CORS dinámico por tenant:** la API solo emite encabezados CORS si el `Origin` de la petición
> coincide con el `DominioPermitido` de un tenant activo.

---

## Estructura del proyecto

```
src/
├── PqrsSaas.Domain/            # Entidades y enums de dominio
├── PqrsSaas.Application/       # Interfaces (ITenantConnectionAccessor)
├── PqrsSaas.Infrastructure/    # EF Core, migraciones, aprovisionamiento, seguridad, Gemini
└── PqrsSaas.Api/               # Controladores, middleware, CORS, Program.cs
frontends/
├── super-admin/                # Dashboard de super administrador (SPA vanilla)
├── agent/                      # Dashboard de agentes (SPA vanilla, por tenant)
├── widget/                     # Widget JS incrustable (pqrs-widget.js + demo.html)
├── nginx.conf + web.Dockerfile # Reverse proxy (rutas /api, /superadmin, /agent, /widget, /)
```

---

## Frontends

Aplicaciones estáticas (JavaScript vanilla, sin build) que se sirven detrás del contenedor **nginx** (`web`).

```
frontends/
├── nginx.conf         # Reverse proxy: rutas /api, /superadmin, /agent, /widget, /
├── web.Dockerfile     # Imagen nginx (nginx:alpine)
├── index.html         # Landing simple
├── super-admin/       # Dashboard de super administrador (SPA)
├── agent/             # Dashboard de agentes (SPA, por tenant)
└── widget/            # Widget JS incrustable (pqrs-widget.js + demo.html)
```

### Rutas servidas por nginx (puerto 8080)

| Ruta | Contenido |
| :--- | :--- |
| `/superadmin/` | Dashboard de super administrador |
| `/agent/` | Dashboard de agentes (login por tenant) |
| `/widget/pqrs-widget.js` | Widget JS incrustable |
| `/widget/demo.html` | Demo del widget |
| `/api/v1/*` | API (proxy a `api:8080`) |
| `/` | Landing |

### Desarrollo local (sin Docker)

Los frontends son estáticos, así que basta un servidor HTTP:

```bash
cd frontends
python -m http.server 5500
```

Y abrir `http://localhost:5500/super-admin/`.

> En desarrollo, `super-admin/js/config.js` detecta el puerto `5500` y apunta la API a
> `http://localhost:5000/api/v1` (la API debe estar corriendo, por Docker o `dotnet run`).
> El CORS de la API ya permite `http://localhost:5500`.

### Notas

- **Auth superadmin:** la SPA guarda el token en una cookie (`sa_token`); se envía como `Authorization: Bearer`.
- **Auth agentes:** login con `slug + email + contraseña` (`POST /auth/login`); guarda token (`ag_token`) y rol/slug (`ag_rol`/`ag_slug`). Si `debeCambiarPassword` es true, se fuerza el cambio de contraseña en el primer login.
- Los dashboards llaman a la API por `/api/v1` relativo (same-origin a través de nginx), así que **no requieren CORS**.
- **Widget:** autocontenido (IIFE + Shadow DOM, sin Tailwind). Se incrusta con `<script src="/widget/pqrs-widget.js" data-tenant="<ApiKeyWidget>" data-api-url="...">`. Corre en el dominio del cliente y usa `X-Tenant-Api-Key` → CORS multi-origen por tenant.

---

## Comandos de desarrollo

```bash
dotnet build                                    # Compilar la solución (net10.0)

# Migración Core (por tenant):
dotnet ef migrations add <Nombre> \
  -c CoreDbContext -p src/PqrsSaas.Infrastructure -s src/PqrsSaas.Api

# Migración Control (catálogo):
dotnet ef migrations add <Nombre> \
  -c ControlDbContext -p src/PqrsSaas.Infrastructure -s src/PqrsSaas.Api

# Aplicar migración de control:
dotnet ef database update -c ControlDbContext -p src/PqrsSaas.Infrastructure -s src/PqrsSaas.Api
```

---

## Estado actual y próximos pasos

- ✅ Backend completo y probado: aprovisionamiento, auth JWT, KB, tickets, RAG, triaje, superadmins, CORS multi-origen, envío de credenciales por SMTP.
- ✅ **Dashboard de super administrador** (`frontends/super-admin/`), en `http://localhost:8080/superadmin/`.
- ✅ **Dashboard de agentes** (`frontends/agent/`), en `http://localhost:8080/agent/` (roles, tickets, KB, gestión de agentes).
- ✅ **Widget JS** (`frontends/widget/`), en `http://localhost:8080/widget/pqrs-widget.js` (chat RAG → formulario → radicado).
- ⏳ **Pendiente:** SignalR (notificaciones en tiempo real).
- Los frontends se sirven por rutas vía nginx (`frontends/nginx.conf`); la API también sigue accesible directa en el puerto 5000 para desarrollo/Swagger.

## Documentación adicional

- [`AGENTS.md`](AGENTS.md) — guía técnica para desarrollo (arquitectura, quirks, comandos).
- [`docs/proyecto.md`](docs/proyecto.md) — requerimientos funcionales.
- [`docs/estructura.md`](docs/estructura.md) — especificación técnica y plan de entrega.
