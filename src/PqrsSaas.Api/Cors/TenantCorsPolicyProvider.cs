using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using PqrsSaas.Infrastructure.Persistence;

namespace PqrsSaas.Api.Cors;

/// <summary>
/// Política de CORS dinámica. Permite un origen si:
///  1. Está en la lista global `Cors:AllowedOrigins` (dashboard de superadmin,
///     entornos de desarrollo, etc.), configurable vía env `CORS_ALLOWED_ORIGINS`, o
///  2. Coincide con el `DominioPermitido` de un tenant activo (widgets de clientes).
/// En cualquier otro caso no se emiten encabezados CORS y el navegador bloquea.
/// </summary>
public class TenantCorsPolicyProvider : ICorsPolicyProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string[] _allowedOrigins;

    public TenantCorsPolicyProvider(IServiceScopeFactory scopeFactory, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _allowedOrigins = (config["Cors:AllowedOrigins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
    {
        var origin = context.Request.Headers["Origin"].FirstOrDefault();
        if (string.IsNullOrEmpty(origin))
            return null;

        // Orígenes permitidos globalmente (p. ej. el dashboard de superadmin).
        if (_allowedOrigins.Contains(origin))
            return BuildPolicy(origin);

        using var scope = _scopeFactory.CreateScope();
        var controlDb = scope.ServiceProvider.GetRequiredService<ControlDbContext>();

        // Coincide si el origen pertenece a un tenant activo. Se revisa la tabla de
        // orígenes (multi-dominio) y también el campo DominioPermitido (backward-compat).
        var tenantExiste = await controlDb.TenantDominios
            .AsNoTracking()
            .AnyAsync(d => d.Origen == origin && d.Tenant.Activo);

        if (!tenantExiste)
            tenantExiste = await controlDb.Tenants
                .AsNoTracking()
                .AnyAsync(t => t.DominioPermitido == origin && t.Activo);

        if (!tenantExiste)
            return null;

        return BuildPolicy(origin);
    }

    private static CorsPolicy BuildPolicy(string origin)
    {
        var policy = new CorsPolicy();
        policy.Origins.Add(origin);
        policy.Headers.Add("*");
        policy.Methods.Add("*");

        return policy;
    }
}
