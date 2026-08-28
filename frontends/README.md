# Frontends

Aplicaciones estáticas (JavaScript vanilla, sin build) que se sirven detrás del contenedor **nginx** (`web`).

## Estructura

```
frontends/
├── nginx.conf         # Reverse proxy: rutas /api, /superadmin, /agent, /widget, /
├── web.Dockerfile     # Imagen nginx (nginx:alpine)
├── index.html         # Landing simple
├── super-admin/       # Dashboard de super administrador (SPA)
├── agent/             # Dashboard de agentes (SPA, por tenant)
└── widget/            # Widget JS incrustable (pqrs-widget.js + demo.html)
```

## Rutas servidas por nginx (puerto 8080)

| Ruta | Contenido |
| :--- | :--- |
| `/superadmin/` | Dashboard de super administrador |
| `/agent/` | Dashboard de agentes (login por tenant) |
| `/widget/pqrs-widget.js` | Widget JS incrustable |
| `/widget/demo.html` | Demo del widget |
| `/api/v1/*` | API (proxy a `api:8080`) |
| `/` | Landing |

## Puesta en marcha (con Docker)

En la raíz del repo:

```bash
docker compose up --build
```

Y abrir `http://localhost:8080/superadmin/`. Credenciales por defecto:
`superadmin@pqrs.local` / `admin123` (configurables con `SUPERADMIN_EMAIL` / `SUPERADMIN_PASSWORD`).

> El primer login crea (bootstrap) el superadmin con esas credenciales; después la contraseña
> se gestiona desde la sección **Configuración** del propio panel.

## Desarrollo local (sin Docker)

Los frontends son estáticos, así que basta un servidor HTTP. Por ejemplo:

```bash
cd frontends
python -m http.server 5500
```

Y abrir `http://localhost:5500/super-admin/`.

> En desarrollo, `super-admin/js/config.js` detecta el puerto `5500` y apunta la API a
> `http://localhost:5000/api/v1` (la API debe estar corriendo, por Docker o `dotnet run`).
> El CORS de la API ya permite `http://localhost:5500`.

## Notas

- **Auth superadmin:** la SPA guarda el token en una cookie (`sa_token`); se envía como `Authorization: Bearer`.
- **Auth agentes:** login con `slug + email + contraseña` (`POST /auth/login`); guarda token (`ag_token`) y rol/slug (`ag_rol`/`ag_slug`). Si `debeCambiarPassword` es true, se fuerza el cambio de contraseña en el primer login.
- Los dashboards llaman a la API por `/api/v1` relativo (same-origin a través de nginx), así que **no requieren CORS**.
- **Widget:** autocontenido (IIFE + Shadow DOM, sin Tailwind). Se incrusta con `<script src="/widget/pqrs-widget.js" data-tenant="<ApiKeyWidget>" data-api-url="...">`. Corre en el dominio del cliente y usa `X-Tenant-Api-Key` → CORS multi-origen por tenant.
