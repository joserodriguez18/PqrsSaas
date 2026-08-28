using Microsoft.EntityFrameworkCore;
using PqrsSaas.Application;
using PqrsSaas.Infrastructure.Persistence;

namespace PqrsSaas.Api.Middleware;

public class TenantResolutionMiddleware
{
    public const string TenantIdKey = "TenantId";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ControlDbContext controlDb,
        ITenantConnectionAccessor accessor,
        IConfiguration config)
    {
        // Fase 1 (widget, sin autenticar): header X-Tenant-Api-Key.
        var apiKey = context.Request.Headers["X-Tenant-Api-Key"].FirstOrDefault();

        // Fase 2 (agentes autenticados): claim "tenantId" del JWT. Requiere que
        // UseAuthentication() ya haya corrido antes que este middleware, para que
        // context.User venga poblado.
        var tenantIdClaim = context.User.FindFirst("tenantId")?.Value;

        if (!string.IsNullOrEmpty(apiKey))
        {
            var tenant = await controlDb.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ApiKeyWidget == apiKey && t.Activo);

            if (tenant is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Tenant no encontrado o inactivo.");
                return;
            }

            var template = config.GetConnectionString("TenantTemplate")!;
            accessor.ConnectionString = template.Replace("{db}", tenant.NombreBaseDatos);
            context.Items[TenantIdKey] = tenant.Id;
        }
        else if (!string.IsNullOrEmpty(tenantIdClaim) && Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            var tenant = await controlDb.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId && t.Activo);

            if (tenant is null)
            {
                // El token es válido pero el tenant fue desactivado después de emitirlo.
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Tenant asociado al token ya no está activo.");
                return;
            }

            var template = config.GetConnectionString("TenantTemplate")!;
            accessor.ConnectionString = template.Replace("{db}", tenant.NombreBaseDatos);
            context.Items[TenantIdKey] = tenant.Id;
        }

        // Si no se resolvió ningún tenant aquí no fallamos: hay rutas (registro de
        // tenant, login) que no dependen de CoreDbContext y resuelven su propia
        // conexión manualmente (ver AuthController.Login). Si un endpoint que sí
        // necesita CoreDbContext se llama sin tenant resuelto, la excepción sale
        // de CoreDbContext.OnConfiguring.

        await _next(context);
    }
}
