using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PqrsSaas.Domain.Entities;
using PqrsSaas.Infrastructure.Persistence;
using PqrsSaas.Infrastructure.Security;
using PqrsSaas.Infrastructure.Services;
using System.IdentityModel.Tokens.Jwt;

namespace PqrsSaas.Api.Controllers;

public record InvitarAgenteRequest(string Email, RolUsuario Rol);
public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);
public record ActualizarEstadoAgenteRequest(bool Activo);

[ApiController]
[Route("api/v1/agents")]
[Authorize]
public class AgentsController : ControllerBase
{
    private readonly CoreDbContext _coreDb;
    private readonly PasswordService _passwordService;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;

    public AgentsController(CoreDbContext coreDb, PasswordService passwordService, IEmailSender emailSender, IConfiguration config)
    {
        _coreDb = coreDb;
        _passwordService = passwordService;
        _emailSender = emailSender;
        _config = config;
    }

    [HttpGet("yo")]
    public async Task<IActionResult> Yo(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var user = await _coreDb.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return NotFound();

        return Ok(new { user.Id, user.Email, Rol = user.Rol.ToString(), user.DebeCambiarPassword });
    }

    /// <summary>Lista los usuarios (agentes/administradores) del tenant. Solo Administradores.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!await EsAdministradorAsync(ct))
            return Forbid();

        var users = await _coreDb.Users
            .AsNoTracking()
            .OrderBy(u => u.FechaCreacion)
            .Select(u => new
            {
                u.Id,
                u.Email,
                Rol = u.Rol.ToString(),
                u.Activo,
                u.DebeCambiarPassword,
                u.FechaCreacion
            })
            .ToListAsync(ct);

        return Ok(users);
    }

    /// <summary>Invita a un nuevo agente. Solo Administradores.</summary>
    [HttpPost("invite")]
    public async Task<IActionResult> Invitar([FromBody] InvitarAgenteRequest request, CancellationToken ct)
    {
        if (!await EsAdministradorAsync(ct))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email obligatorio.");

        if (await _coreDb.Users.AnyAsync(u => u.Email == request.Email, ct))
            return Conflict("Ya existe un usuario con ese correo.");

        var password = GenerarPasswordTemporal();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim(),
            Rol = request.Rol,
            DebeCambiarPassword = true
        };
        user.PasswordHash = _passwordService.Hash(user, password);

        _coreDb.Users.Add(user);
        await _coreDb.SaveChangesAsync(ct);

        var tenantSlug = User.FindFirst("tenantSlug")?.Value;
        var panelBase = _config["App:PanelBaseUrl"] ?? "http://localhost:8080";
        var enviado = await _emailSender.EnviarAsync(
            user.Email,
            "Tus credenciales de acceso · PQRS SaaS",
            CredentialEmailBuilder.Bienvenida(user.Email, user.Email, password, tenantSlug, $"{panelBase}/agent/"),
            ct);

        var respuesta = enviado
            ? (object)new
            {
                user.Id,
                user.Email,
                user.Rol,
                enviadasPorCorreo = true,
                aviso = "Las credenciales fueron enviadas al correo del agente. Debe cambiarlas en su primer ingreso."
            }
            : (object)new
            {
                user.Id,
                user.Email,
                user.Rol,
                password,
                aviso = "SMTP no configurado: esta contraseña solo se muestra una vez."
            };

        return CreatedAtAction(nameof(Yo), new { id = user.Id }, respuesta);
    }

    /// <summary>Cambia la contraseña del propio usuario autenticado.</summary>
    [HttpPut("me/password")]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var user = await _coreDb.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return NotFound();

        if (!_passwordService.Verify(user, request.PasswordActual))
            return Unauthorized("Contraseña actual incorrecta.");

        if (string.IsNullOrWhiteSpace(request.PasswordNueva) || request.PasswordNueva.Length < 6)
            return BadRequest("La nueva contraseña debe tener al menos 6 caracteres.");

        user.PasswordHash = _passwordService.Hash(user, request.PasswordNueva);
        user.DebeCambiarPassword = false;
        await _coreDb.SaveChangesAsync(ct);

        return Ok(new { message = "Contraseña actualizada correctamente." });
    }

    /// <summary>Desactiva a un agente. Solo Administradores.</summary>
    [HttpPut("{id:guid}/desactivar")]
    public async Task<IActionResult> Desactivar(Guid id, CancellationToken ct)
    {
        if (!await EsAdministradorAsync(ct))
            return Forbid();

        var user = await _coreDb.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return NotFound();

        user.Activo = false;
        await _coreDb.SaveChangesAsync(ct);

        return Ok(new { user.Id, user.Email, user.Activo });
    }

    /// <summary>Activa o desactiva un agente. Solo Administradores.</summary>
    [HttpPut("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] ActualizarEstadoAgenteRequest request, CancellationToken ct)
    {
        if (!await EsAdministradorAsync(ct))
            return Forbid();

        var user = await _coreDb.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return NotFound();

        user.Activo = request.Activo;
        await _coreDb.SaveChangesAsync(ct);

        return Ok(new { user.Id, user.Email, user.Activo });
    }

    private async Task<bool> EsAdministradorAsync(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var user = await _coreDb.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is { Rol: RolUsuario.Administrador };
    }

    private static string GenerarPasswordTemporal()
    {
        return Guid.NewGuid().ToString("N")[..12];
    }
}
