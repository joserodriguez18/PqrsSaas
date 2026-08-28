using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PqrsSaas.Domain.Entities;
using PqrsSaas.Infrastructure.Persistence;
using PqrsSaas.Infrastructure.Security;
using System.IdentityModel.Tokens.Jwt;

namespace PqrsSaas.Api.Controllers;

public record LoginSuperAdminRequest(string Email, string Password);
public record ActualizarEstadoTenantRequest(bool Activo);
public record RegistrarSuperAdminRequest(string Email, string Password);
public record CambiarPasswordSuperAdminRequest(string PasswordActual, string PasswordNueva);

[ApiController]
[Route("api/v1")]
public class SuperAdminController : ControllerBase
{
    private readonly ControlDbContext _controlDb;
    private readonly TokenService _tokenService;
    private readonly PasswordService _passwordService;
    private readonly IConfiguration _config;

    public SuperAdminController(
        ControlDbContext controlDb,
        TokenService tokenService,
        PasswordService passwordService,
        IConfiguration config)
    {
        _controlDb = controlDb;
        _tokenService = tokenService;
        _passwordService = passwordService;
        _config = config;
    }

    /// <summary>
    /// Login del super administrador contra la tabla SuperAdmins. Si aún no
    /// existe ningún superadmin, se "siembra" el primero usando las credenciales
    /// de configuración (SuperAdmin:Email + SuperAdmin:Password) — bootstrap único.
    /// </summary>
    [HttpPost("auth/login-superadmin")]
    public async Task<IActionResult> Login([FromBody] LoginSuperAdminRequest request, CancellationToken ct)
    {
        var tieneSuperAdmins = await _controlDb.SuperAdmins.AnyAsync(ct);

        SuperAdmin? sa;
        if (!tieneSuperAdmins)
        {
            sa = await BootstrapPrimeroAsync(request.Email, request.Password, ct);
            if (sa is null)
                return Unauthorized("Credenciales de super administrador inválidas.");
        }
        else
        {
            sa = await _controlDb.SuperAdmins
                .FirstOrDefaultAsync(s => s.Email == request.Email.Trim() && s.Activo, ct);
            if (sa is null || !_passwordService.Verify(sa, request.Password))
                return Unauthorized("Credenciales de super administrador inválidas.");
        }

        return Ok(new { token = _tokenService.GenerarTokenSuperAdmin(sa) });
    }

    /// <summary>Cambia la contraseña del propio superadmin autenticado.</summary>
    [HttpPut("superadmins/me/password")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordSuperAdminRequest request, CancellationToken ct)
    {
        var superAdminId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var sa = await _controlDb.SuperAdmins.FirstOrDefaultAsync(s => s.Id == superAdminId, ct);
        if (sa is null)
            return NotFound();

        if (!_passwordService.Verify(sa, request.PasswordActual))
            return Unauthorized("Contraseña actual incorrecta.");

        if (string.IsNullOrWhiteSpace(request.PasswordNueva) || request.PasswordNueva.Length < 6)
            return BadRequest("La nueva contraseña debe tener al menos 6 caracteres.");

        sa.PasswordHash = _passwordService.Hash(sa, request.PasswordNueva);
        await _controlDb.SaveChangesAsync(ct);

        return Ok(new { message = "Contraseña actualizada correctamente." });
    }

    /// <summary>Registra a otro super administrador. Solo SuperAdmins.</summary>
    [HttpPost("superadmins")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Registrar([FromBody] RegistrarSuperAdminRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Email y contraseña son obligatorios.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _controlDb.SuperAdmins.AnyAsync(s => s.Email == email, ct))
            return Conflict("Ya existe un super administrador con ese correo.");

        var sa = new SuperAdmin { Id = Guid.NewGuid(), Email = email };
        sa.PasswordHash = _passwordService.Hash(sa, request.Password);

        _controlDb.SuperAdmins.Add(sa);
        await _controlDb.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Login), new { email = sa.Email }, new { sa.Id, sa.Email, sa.Activo });
    }

    [HttpGet("superadmins")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var admins = await _controlDb.SuperAdmins
            .AsNoTracking()
            .OrderBy(s => s.FechaCreacion)
            .Select(s => new { s.Id, s.Email, s.Activo, s.FechaCreacion })
            .ToListAsync(ct);

        return Ok(admins);
    }

    private async Task<SuperAdmin?> BootstrapPrimeroAsync(string email, string password, CancellationToken ct)
    {
        var configEmail = (_config["SuperAdmin:Email"] ?? "superadmin@pqrs.local").Trim();
        var configPassword = _config["SuperAdmin:Password"];

        if (string.IsNullOrEmpty(configPassword) ||
            !string.Equals(configPassword, password, StringComparison.Ordinal) ||
            !string.Equals(configEmail, email.Trim(), StringComparison.OrdinalIgnoreCase))
            return null;

        var sa = new SuperAdmin { Id = Guid.NewGuid(), Email = configEmail.ToLowerInvariant() };
        sa.PasswordHash = _passwordService.Hash(sa, configPassword);

        _controlDb.SuperAdmins.Add(sa);
        await _controlDb.SaveChangesAsync(ct);

        return sa;
    }

    [HttpGet("tenants")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ListTenants(CancellationToken ct)
    {
        var tenants = await _controlDb.Tenants
            .AsNoTracking()
            .OrderBy(t => t.FechaCreacion)
            .Select(t => new
            {
                t.Id,
                t.Nombre,
                t.Slug,
                t.DominioPermitido,
                Dominios = t.Dominios.Select(d => d.Origen).ToList(),
                t.ApiKeyWidget,
                t.NombreBaseDatos,
                t.EstadoProvisionamiento,
                t.Activo,
                t.FechaCreacion
            })
            .ToListAsync(ct);

        return Ok(tenants);
    }

    [HttpPut("tenants/{id:guid}/estado")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CambiarEstadoTenant(Guid id, [FromBody] ActualizarEstadoTenantRequest request, CancellationToken ct)
    {
        var tenant = await _controlDb.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tenant is null)
            return NotFound();

        tenant.Activo = request.Activo;
        await _controlDb.SaveChangesAsync(ct);

        return Ok(new { tenant.Id, tenant.Nombre, tenant.Activo });
    }
}
