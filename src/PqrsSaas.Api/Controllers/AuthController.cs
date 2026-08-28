using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PqrsSaas.Application;
using PqrsSaas.Infrastructure.Persistence;
using PqrsSaas.Infrastructure.Security;

namespace PqrsSaas.Api.Controllers;

public record LoginRequest(string TenantSlug, string Email, string Password);

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly ControlDbContext _controlDb;
    private readonly CoreDbContext _coreDb;
    private readonly ITenantConnectionAccessor _tenantConnectionAccessor;
    private readonly IConfiguration _config;
    private readonly PasswordService _passwordService;
    private readonly TokenService _tokenService;

    public AuthController(
        ControlDbContext controlDb,
        CoreDbContext coreDb,
        ITenantConnectionAccessor tenantConnectionAccessor,
        IConfiguration config,
        PasswordService passwordService,
        TokenService tokenService)
    {
        _controlDb = controlDb;
        _coreDb = coreDb;
        _tenantConnectionAccessor = tenantConnectionAccessor;
        _config = config;
        _passwordService = passwordService;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var tenant = await _controlDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == request.TenantSlug && t.Activo, ct);

        if (tenant is null)
            return Unauthorized("Tenant no encontrado o inactivo.");

        // A diferencia del widget (resuelto por el middleware vía API key),
        // el login de agentes resuelve la conexión aquí mismo con el slug
        // recibido en el body, ANTES de que exista cualquier token.
        var template = _config.GetConnectionString("TenantTemplate")!;
        _tenantConnectionAccessor.ConnectionString = template.Replace("{db}", tenant.NombreBaseDatos);

        var user = await _coreDb.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user is null || !user.Activo || !_passwordService.Verify(user, request.Password))
            return Unauthorized("Credenciales inválidas.");

        var token = _tokenService.GenerarToken(user, tenant);

        return Ok(new
        {
            token,
            usuario = new { user.Id, user.Email, Rol = user.Rol.ToString(), user.DebeCambiarPassword },
            tenant = new { tenant.Id, tenant.Slug, tenant.Nombre }
        });
    }
}
