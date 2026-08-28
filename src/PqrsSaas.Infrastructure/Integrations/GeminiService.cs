using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace PqrsSaas.Infrastructure.Integrations;

/// <summary>
/// Cliente delgado hacia la API de Google Gemini (generativelanguage.googleapis.com).
/// Expone dos capacidades usadas por el pipeline de IA:
///   1. GenerarEmbeddingAsync  -> modelo de embeddings (gemini-embedding-001).
///   2. GenerarTextoAsync       -> modelo generativo (gemini-2.5-flash-lite) para
///                                 triaje estructurado y síntesis de respuestas RAG.
/// La API key se lee de la configuración (Gemini:ApiKey), provista vía env
/// GEMINI_API_KEY en docker-compose.
/// </summary>
public class GeminiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _embeddingModel;
    private readonly string _generationModel;

    public GeminiService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Falta Gemini:ApiKey en la configuración.");
        _embeddingModel = config["Gemini:EmbeddingModel"] ?? "gemini-embedding-001";
        _generationModel = config["Gemini:GenerationModel"] ?? "gemini-2.5-flash-lite";
    }

    /// <summary>
    /// Genera el vector de embedding (768 dimensiones) para un texto dado.
    /// </summary>
    public async Task<float[]> GenerarEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["model"] = $"models/{_embeddingModel}",
            ["content"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = text } } },
            ["outputDimensionality"] = 768
        };

        var node = await PostAsync($"/v1beta/models/{_embeddingModel}:embedContent", body, ct);

        var values = node?["embedding"]?["values"]?.AsArray();
        if (values is null)
            throw new InvalidOperationException("Gemini no devolvió embedding.");

        var floats = new float[values.Count];
        for (var i = 0; i < values.Count; i++)
            floats[i] = values[i]!.GetValue<float>();

        return floats;
    }

    /// <summary>
    /// Llama al modelo generativo con un prompt y devuelve el texto de respuesta.
    /// </summary>
    public async Task<string> GenerarTextoAsync(string prompt, CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = prompt } } }
            },
            ["generationConfig"] = new JsonObject { ["temperature"] = 0.3 }
        };

        var node = await PostAsync($"/v1beta/models/{_generationModel}:generateContent", body, ct);

        var text = node?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini no devolvió contenido de texto.");

        return text.Trim();
    }

    private async Task<JsonNode?> PostAsync(string path, JsonObject body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com{path}?key={_apiKey}");
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Gemini respondió {(int)response.StatusCode}: {raw}");

        return JsonNode.Parse(raw);
    }
}
