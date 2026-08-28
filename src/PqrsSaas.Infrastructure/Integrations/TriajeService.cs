using System.Text.Json;
using System.Text.RegularExpressions;
using PqrsSaas.Domain.Entities;

namespace PqrsSaas.Infrastructure.Integrations;

public record TriajeResult(TipoPqrs Tipo, PrioridadTicket Prioridad, SentimientoTicket Sentimiento, string Resumen);

/// <summary>
/// Ejecuta el triaje automático de una PQRS recién radicada: pide a Gemini un
/// análisis estructurado (Tipo, Prioridad, Sentimiento, Resumen) y lo mapea a
/// los enums del dominio. El texto se vuelve a generar por si Gemini envuelve
/// el JSON en bloques de código (```json ... ```) o agrega texto alrededor.
/// </summary>
public class TriajeService
{
    private readonly GeminiService _gemini;

    public TriajeService(GeminiService gemini)
    {
        _gemini = gemini;
    }

    public async Task<TriajeResult> TriarAsync(string asunto, string descripcion, CancellationToken ct = default)
    {
        var prompt =
            "Eres un sistema de clasificación de PQRS (Peticiones, Quejas, Reclamos y Sugerencias). " +
            "Analiza el ASUNTO y la DESCRIPCIÓN del ciudadano y devuelve EXCLUSIVAMENTE un JSON válido " +
            "con este esquema (sin texto adicional, sin markdown):\n" +
            "{\"Tipo\":\"Peticion|Queja|Reclamo|Sugerencia\",\"Prioridad\":\"Baja|Media|Alta\"," +
            "\"Sentimiento\":\"Positivo|Neutro|Negativo\",\"Resumen\":\"1 o 2 oraciones\"}\n\n" +
            "Criterios: Reclamo y queja severa o insatisfacción crítica -> Prioridad Alta; " +
            "peticiones estándar -> Media; consultas y sugerencias -> Baja.\n\n" +
            $"ASUNTO: {asunto}\nDESCRIPCIÓN: {descripcion}";

        var raw = await _gemini.GenerarTextoAsync(prompt, ct);
        return ParseTriaje(raw);
    }

    private static TriajeResult ParseTriaje(string raw)
    {
        var cleaned = raw.Trim();

        var fenced = Regex.Match(cleaned, @"```(?:json)?\s*([\s\S]*?)```");
        if (fenced.Success)
            cleaned = fenced.Groups[1].Value.Trim();

        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        if (start >= 0 && end > start)
            cleaned = cleaned[start..(end + 1)];

        using var doc = JsonDocument.Parse(cleaned);
        var root = doc.RootElement;

        return new TriajeResult(
            Tipo: ParseEnum<TipoPqrs>(root.GetProperty("Tipo").GetString()),
            Prioridad: ParseEnum<PrioridadTicket>(root.GetProperty("Prioridad").GetString()),
            Sentimiento: ParseEnum<SentimientoTicket>(root.GetProperty("Sentimiento").GetString()),
            Resumen: root.GetProperty("Resumen").GetString() ?? "");
    }

    private static T ParseEnum<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : default;
}
