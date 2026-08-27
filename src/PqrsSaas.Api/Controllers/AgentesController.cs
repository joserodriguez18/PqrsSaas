using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PqrsSaas.Infrastructure.Persistence;

namespace PqrsSaas.Api.Controllers;

/// <summary>
/// Endpoint mínimo para validar el flujo completo: JWT válido -> middleware
/// resuelve el tenant desde el claim -> CoreDbContext apunta a la BD correcta.
/// El CRUD real de kb-articles y tickets se construye en los módulos de
/// RAG y Triaje (comparten este mismo patrón de autenticación).
/// </summary>
[ApiController]
[Route("api/v1/agentes")]
[Authorize]
public class AgentesController : ControllerBase
{
    private readonly CoreDbContext _coreDb;

    public AgentesController(CoreDbContext coreDb)
    {
        _coreDb = coreDb;
    }

    [HttpGet("yo")]
    public async Task<IActionResult> Yo(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!.Value);
        var user = await _coreDb.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return NotFound();

        return Ok(new { user.Id, user.Email, Rol = user.Rol.ToString() });
    }
}
