using Microsoft.EntityFrameworkCore;
using PqrsSaas.Infrastructure.Persistence;

namespace PqrsSaas.Infrastructure.Integrations;

/// <summary>
/// Genera el número de radicado de un ticket con formato PQRS-YYYY-NNNN.
/// Se calcula como el conteo de tickets radicados en el año actual + 1.
/// Adecuado para el MVP (no es a prueba de concurrencia).
/// </summary>
public static class TicketNumberService
{
    public static async Task<string> GenerarAsync(CoreDbContext db, CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var count = await db.Tickets
            .CountAsync(t => t.FechaCreacion.Year == year, ct);
        return $"PQRS-{year}-{(count + 1):D4}";
    }
}
