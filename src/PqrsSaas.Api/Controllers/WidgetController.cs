using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using PqrsSaas.Api.Hubs;
using PqrsSaas.Api.Middleware;
using PqrsSaas.Domain.Entities;
using PqrsSaas.Infrastructure.Integrations;
using PqrsSaas.Infrastructure.Persistence;

namespace PqrsSaas.Api.Controllers;

public record RagSearchRequest(string Consulta);
public record WidgetTicketRequest(string ClienteNombre, string ClienteCorreo, string Asunto, string Descripcion);
public record RagArticulo(string Pregunta, string Respuesta, double Similitud);

/// <summary>
/// Endpoints públicos del widget. El tenant se resuelve mediante el header
/// X-Tenant-Api-Key (ver TenantResolutionMiddleware); no requieren JWT.
/// </summary>
[ApiController]
[Route("api/v1/widget")]
public class WidgetController : ControllerBase
{
    private readonly CoreDbContext _coreDb;
    private readonly GeminiService _gemini;
    private readonly TriajeService _triaje;
    private readonly IConfiguration _config;
    private readonly IHubContext<TicketsHub> _hub;

    public WidgetController(CoreDbContext coreDb, GeminiService gemini, TriajeService triaje, IConfiguration config, IHubContext<TicketsHub> hub)
    {
        _coreDb = coreDb;
        _gemini = gemini;
        _triaje = triaje;
        _config = config;
        _hub = hub;
    }

    /// <summary>
    /// Auto-atención: busca en la base de conocimiento del tenant por similitud
    /// de coseno y, si supera el umbral, sintetiza una respuesta con el LLM.
    /// </summary>
    [HttpPost("rag-search")]
    public async Task<IActionResult> RagSearch([FromBody] RagSearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Consulta))
            return BadRequest("Consulta obligatoria.");

        if (EsSaludo(request.Consulta))
            return Ok(new
            {
                encontrado = false,
                saludo = true,
                respuesta = "¡Hola! Soy tu asistente virtual. ¿En qué puedo ayudarte hoy? Puedes preguntarme sobre nuestros servicios o radicar una solicitud si lo necesitas.",
                sintetizada = false,
                coincidencias = Array.Empty<RagArticulo>()
            });

        var umbral = _config.GetValue<double>("Rag:UmbralSimilitud", 0.75);
        var queryVector = new Vector(await EmbeddingConReintentoAsync(request.Consulta, ct));

        var crudos = await _coreDb.KnowledgeBaseArticles
            .AsNoTracking()
            .OrderBy(a => a.Embedding.CosineDistance(queryVector))
            .Take(5)
            .Select(a => new
            {
                a.Pregunta,
                a.Respuesta,
                Distancia = a.Embedding.CosineDistance(queryVector)
            })
            .ToListAsync(ct);

        var candidatos = crudos
            .Select(a => new RagArticulo(a.Pregunta, a.Respuesta, 1d - (double)a.Distancia))
            .Where(a => a.Similitud >= umbral)
            .OrderByDescending(a => a.Similitud)
            .Take(3)
            .ToList();

        if (candidatos.Count == 0)
            return Ok(new
            {
                encontrado = false,
                saludo = false,
                respuesta = (string?)null,
                sintetizada = false,
                coincidencias = Array.Empty<RagArticulo>()
            });

        // Síntesis resiliente: si Gemini (generación) está saturado, se devuelve
        // el texto del mejor artículo en lugar de fallar toda la búsqueda.
        try
        {
            var sintetizada = await SintetizarRespuestaAsync(request.Consulta, candidatos, ct);
            return Ok(new { encontrado = true, saludo = false, respuesta = sintetizada, sintetizada = true, coincidencias = candidatos });
        }
        catch
        {
            return Ok(new { encontrado = true, saludo = false, respuesta = candidatos[0].Respuesta, sintetizada = false, coincidencias = candidatos });
        }
    }

    /// <summary>
    /// Detecta saludos/cortesía para responder con un saludo amable sin pasar por Gemini.
    /// Regla conservadora: si la consulta contiene contenido sustantivo (una pregunta real),
    /// no se trata como saludo y va al RAG.
    /// </summary>
    private static bool EsSaludo(string consulta)
    {
        var texto = System.Text.RegularExpressions.Regex.Replace(consulta.ToLowerInvariant(), @"[^a-záéíóúñ\s]", " ")
            .Trim();
        if (texto.Length == 0) return false;

        var tokens = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 4) return false;

        var cortesia = new HashSet<string>
        {
            "hola", "buenas", "buenos", "dias", "tardes", "noches", "saludos", "saludo",
            "gracias", "hey", "que", "tal", "como", "estas", "buen"
        };

        // Todos los tokens deben ser de cortesía o conectores triviales.
        return tokens.All(cortesia.Contains);
    }

    /// <summary>
    /// Genera el embedding de la consulta con reintentos ante 503 puntuales de Gemini.
    /// </summary>
    private async Task<float[]> EmbeddingConReintentoAsync(string text, CancellationToken ct)
    {
        var delay = new[] { 500, 1000, 2000 };
        for (var intento = 0; ; intento++)
        {
            try
            {
                return await _gemini.GenerarEmbeddingAsync(text, ct);
            }
            catch when (intento < delay.Length)
            {
                await Task.Delay(delay[intento], ct);
            }
        }
    }

    /// <summary>
    /// Radicación formal de una PQRS con triaje automático de IA.
    /// </summary>
    [HttpPost("tickets")]
    public async Task<IActionResult> CrearTicket([FromBody] WidgetTicketRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ClienteNombre) ||
            string.IsNullOrWhiteSpace(request.ClienteCorreo) ||
            string.IsNullOrWhiteSpace(request.Asunto) ||
            string.IsNullOrWhiteSpace(request.Descripcion))
            return BadRequest("Todos los campos son obligatorios.");

        var triaje = await TriajeConReintentoAsync(request.Asunto, request.Descripcion, ct);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            NumeroRadicado = await TicketNumberService.GenerarAsync(_coreDb, ct),
            ClienteNombre = request.ClienteNombre.Trim(),
            ClienteCorreo = request.ClienteCorreo.Trim(),
            Asunto = request.Asunto.Trim(),
            Descripcion = request.Descripcion.Trim(),
            Tipo = triaje.Tipo,
            Prioridad = triaje.Prioridad,
            Sentimiento = triaje.Sentimiento,
            Resumen = triaje.Resumen,
            Estado = EstadoTicket.Pendiente,
            ResueltoPorRAG = false
        };

        _coreDb.Tickets.Add(ticket);
        await _coreDb.SaveChangesAsync(ct);

        // Notifica en tiempo real a los agentes de este tenant (grupo tenant-<id>).
        if (HttpContext.Items.TryGetValue(TenantResolutionMiddleware.TenantIdKey, out var tenantIdObj) &&
            tenantIdObj is Guid tenantId)
        {
            await _hub.Clients.Group(TicketsHub.GrupoTenant(tenantId.ToString())).SendAsync("TicketNuevo", new
            {
                ticket.Id,
                ticket.NumeroRadicado,
                ticket.ClienteNombre,
                ticket.ClienteCorreo,
                ticket.Asunto,
                ticket.Tipo,
                ticket.Prioridad,
                ticket.Sentimiento,
                ticket.Estado
            }, ct);
        }

        return CreatedAtAction(nameof(CrearTicket), new { id = ticket.Id }, new
        {
            ticket.Id,
            ticket.NumeroRadicado,
            ticket.Tipo,
            ticket.Prioridad,
            ticket.Sentimiento,
            ticket.Resumen,
            ticket.Estado
        });
    }

    /// <summary>
    /// Ejecuta el triaje con reintentos y, si Gemini sigue caído, cae a valores por
    /// defecto para que la radicación NUNCA falle (un agente ajusta la clasificación).
    /// </summary>
    private async Task<TriajeResult> TriajeConReintentoAsync(string asunto, string descripcion, CancellationToken ct)
    {
        var delay = new[] { 500, 1000, 2000 };
        for (var intento = 0; ; intento++)
        {
            try
            {
                return await _triaje.TriarAsync(asunto, descripcion, ct);
            }
            catch when (intento < delay.Length)
            {
                await Task.Delay(delay[intento], ct);
            }
            catch
            {
                // Gemini inaccesible: radicar con clasificación por defecto.
                return new TriajeResult(TipoPqrs.Peticion, PrioridadTicket.Media, SentimientoTicket.Neutro, null);
            }
        }
    }

    private async Task<string> SintetizarRespuestaAsync(
        string consulta,
        List<RagArticulo> articulos,
        CancellationToken ct)
    {
        var contexto = string.Join("\n\n", articulos.Select(a => $"P: {a.Pregunta}\nR: {a.Respuesta}"));

        var prompt =
            "Eres el asistente virtual de atención al cliente de esta empresa. Responde en español, de forma " +
            "amable, clara y breve (2 a 3 oraciones). Usa ÚNICAMENTE la información de los artículos de la " +
            "base de conocimiento proporcionados. Si la consulta no se puede responder con esos artículos, " +
            "indícalo con honestidad y ofrece ayuda al usuario, sin inventar datos.\n\n" +
            $"ARTÍCULOS:\n{contexto}\n\nCONSULTA DEL USUARIO: {consulta}";

        return await _gemini.GenerarTextoAsync(prompt, ct);
    }
}
