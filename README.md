# Esqueleto — Módulos 1, 2 y 3 (Infraestructura, Aprovisionamiento, Auth JWT)

Este zip trae la estructura de capas y el código del flujo de aprovisionamiento dinámico
de tenants (crear BD + migrar con EF Core en runtime), listo para que DeepSeek continúe
desde aquí. No se pudo compilar ni restaurar paquetes en el entorno donde se generó este
esqueleto (sin SDK de .NET ni acceso a NuGet), así que el primer paso real es verificar
que compila en tu máquina.

## Primeros comandos (en tu entorno local, con dotnet SDK instalado)

```bash
cd PqrsSaas

# 1. Crear el .sln y agregar los proyectos
dotnet new sln -n PqrsSaas
dotnet sln add src/PqrsSaas.Domain/PqrsSaas.Domain.csproj
dotnet sln add src/PqrsSaas.Application/PqrsSaas.Application.csproj
dotnet sln add src/PqrsSaas.Infrastructure/PqrsSaas.Infrastructure.csproj
dotnet sln add src/PqrsSaas.Api/PqrsSaas.Api.csproj

# 2. Restaurar paquetes (revisa que las versiones de Npgsql/Pgvector en el .csproj
#    de Infrastructure existan; si NuGet ya tiene versiones más nuevas, actualízalas)
dotnet restore

# 3. Generar la migración inicial de CoreDbContext (Users, KnowledgeBaseArticles, Tickets)
dotnet ef migrations add InicialCore \
  -c CoreDbContext \
  -p src/PqrsSaas.Infrastructure \
  -s src/PqrsSaas.Api

# 4. Generar la migración de ControlDbContext (Tenants, TenantConfiguraciones)
dotnet ef migrations add InicialControl \
  -c ControlDbContext \
  -p src/PqrsSaas.Infrastructure \
  -s src/PqrsSaas.Api

# 5. Levantar todo
docker-compose up --build
```

## Lo que falta validar primero (módulo de mayor riesgo)

1. Que el usuario `postgres` del contenedor tenga permiso `CREATEDB` (por defecto en la
   imagen oficial sí lo tiene, pero verifícalo).
2. Que `TenantProvisioningService.ProvisionAsync` realmente cree la base, habilite
   `vector` y aplique la migración `InicialCore` sin errores contra Postgres en Docker.
3. Probar el flujo completo pegándole a `POST /api/v1/tenants/registro` con un body
   `{ "nombre": "Empresa Demo", "dominioPermitido": "https://demo.com" }` y confirmar
   que aparece la base `pqrs_tenant_empresa_demo` en Postgres.

Si esto no funciona en la primera hora de trabajo, activa el Plan B documentado en
`pqrs-saas-arquitectura.md` (sección 3): pool de bases pre-creadas.

## Módulo 3 — Auth JWT: cómo quedó armado

- Al registrar un tenant (`POST /api/v1/tenants/registro`) ahora también se siembra un
  usuario **Administrador** inicial en la base del tenant. El body pasa a ser:
  ```json
  { "nombre": "Empresa Demo", "dominioPermitido": "https://demo.com", "emailAdministrador": "admin@demo.com" }
  ```
  La respuesta trae la contraseña generada **una sola vez** — guárdala, no se puede
  recuperar después (queda hasheada en la BD).
- Login: `POST /api/v1/auth/login` con `{ "tenantSlug": "empresa_demo", "email": "...", "password": "..." }`
  devuelve un JWT con claims `tenantId` y `tenantSlug`.
- Cualquier endpoint con `[Authorize]` (ver `AgentesController.Yo` como ejemplo) ya
  resuelve automáticamente la base del tenant correcto a partir del claim del token —
  no hace falta mandar `X-Tenant-Api-Key` en rutas de agente autenticado, solo el
  `Authorization: Bearer <token>`.
- **Decisión de alcance:** no se construyó CRUD completo de agentes (crear/editar/borrar
  otros agentes) — solo login + el admin sembrado al registrar el tenant. El CRUD real
  de `kb-articles` y `tickets` se construye en los módulos de RAG y Triaje, reutilizando
  este mismo patrón de `[Authorize]` + `CoreDbContext` ya resuelto.
- Falta definir `JWT_SECRET` como variable de entorno antes de levantar `docker-compose up`
  (mínimo 32 caracteres aleatorios). Ejemplo rápido para generarlo:
  ```bash
  export JWT_SECRET=$(openssl rand -base64 48)
  ```

## Qué NO está incluido todavía (siguientes módulos)

- CRUD completo de kb-articles / tickets
- Integración con Gemini (RAG + Triaje)
- Hub de SignalR
- Widget JS

Se documentaron como `// TODO` en `Program.cs` en los puntos donde se conectan.
