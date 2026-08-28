using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PqrsSaas.Api.Hubs;
using PqrsSaas.Domain.Entities;
using PqrsSaas.Infrastructure.Integrations;
using PqrsSaas.Infrastructure.Persistence;

namespace PqrsSaas.Api.Controllers;

public record ActualizarEstadoRequest(EstadoTicket Estado);
public record ActualizarPrioridadRequest(PrioridadTicket Prioridad);

[ApiController]
[Route("api/v1/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly CoreDbContext _coreDb;
    private readonly IHubContext<TicketsHub> _hub;

    public TicketsController(CoreDbContext coreDb, IHubContext<TicketsHub> hub)
    {
        _coreDb = coreDb;
        _hub = hub;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] EstadoTicket? estado = null,
        [FromQuery] PrioridadTicket? prioridad = null,
        CancellationToken ct = default)
    {
        var query = _coreDb.Tickets.AsNoTracking().AsQueryable();

        if (estado.HasValue)
            query = query.Where(t => t.Estado == estado.Value);
        if (prioridad.HasValue)
            query = query.Where(t => t.Prioridad == prioridad.Value);

        var tickets = await query
            .OrderByDescending(t => t.FechaCreacion)
            .Select(t => new
            {
                t.Id,
                t.NumeroRadicado,
                t.ClienteNombre,
                t.ClienteCorreo,
                t.Asunto,
                t.Descripcion,
                t.Tipo,
                t.Prioridad,
                t.Sentimiento,
                t.Resumen,
                t.Estado,
                t.ResueltoPorRAG,
                t.FechaCreacion,
                t.FechaActualizacion
            })
            .ToListAsync(ct);

        return Ok(tickets);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var ticket = await _coreDb.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return NotFound();

        return Ok(ticket);
    }

    [HttpPut("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] ActualizarEstadoRequest request, CancellationToken ct)
    {
        var ticket = await _coreDb.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return NotFound();

        ticket.Estado = request.Estado;
        ticket.FechaActualizacion = DateTime.UtcNow;
        await _coreDb.SaveChangesAsync(ct);

        await NotificarActualizacionAsync(ticket, ct);

        return Ok(ticket);
    }

    [HttpPut("{id:guid}/prioridad")]
    public async Task<IActionResult> CambiarPrioridad(Guid id, [FromBody] ActualizarPrioridadRequest request, CancellationToken ct)
    {
        var ticket = await _coreDb.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null)
            return NotFound();

        ticket.Prioridad = request.Prioridad;
        ticket.FechaActualizacion = DateTime.UtcNow;
        await _coreDb.SaveChangesAsync(ct);

        await NotificarActualizacionAsync(ticket, ct);

        return Ok(ticket);
    }

    /// <summary>
    /// Emite el evento TicketActualizado a los agentes del mismo tenant que el
    /// usuario autenticado (claim tenantId), para sincronizar la vista en vivo.
    /// </summary>
    private async Task NotificarActualizacionAsync(Ticket ticket, CancellationToken ct)
    {
        var tenantId = User.FindFirst("tenantId")?.Value;
        if (Guid.TryParse(tenantId, out _))
        {
            await _hub.Clients.Group(TicketsHub.GrupoTenant(tenantId!)).SendAsync("TicketActualizado", new
            {
                ticket.Id,
                ticket.NumeroRadicado,
                ticket.Estado,
                ticket.Prioridad,
                ticket.FechaActualizacion
            }, ct);
        }
    }
}
