using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using PqrsSaas.Domain.Entities;
using PqrsSaas.Infrastructure.Integrations;
using PqrsSaas.Infrastructure.Persistence;
using System.Text.RegularExpressions;

namespace PqrsSaas.Api.Controllers;

public record KbArticleRequest(string Pregunta, string Respuesta);

[ApiController]
[Route("api/v1/kb-articles")]
[Authorize]
public class KnowledgeBaseArticlesController : ControllerBase
{
    private const long MaxFileBytes = 5 * 1024 * 1024; // 5 MB

    private readonly CoreDbContext _coreDb;
    private readonly GeminiService _gemini;
    private readonly DocumentIngestionService _ingestion;

    public KnowledgeBaseArticlesController(CoreDbContext coreDb, GeminiService gemini, DocumentIngestionService ingestion)
    {
        _coreDb = coreDb;
        _gemini = gemini;
        _ingestion = ingestion;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var articles = await _coreDb.KnowledgeBaseArticles
            .AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Select(a => new { a.Id, a.Pregunta, a.Respuesta, a.Titulo })
            .ToListAsync(ct);

        return Ok(articles);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] KbArticleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Pregunta) || string.IsNullOrWhiteSpace(request.Respuesta))
            return BadRequest("Pregunta y Respuesta son obligatorias.");

        var embedding = await _gemini.GenerarEmbeddingAsync($"{request.Pregunta}\n{request.Respuesta}", ct);

        var article = new KnowledgeBaseArticle
        {
            Id = Guid.NewGuid(),
            Pregunta = request.Pregunta.Trim(),
            Respuesta = request.Respuesta.Trim(),
            Embedding = new Vector(embedding)
        };

        _coreDb.KnowledgeBaseArticles.Add(article);
        await _coreDb.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = article.Id },
            new { article.Id, article.Pregunta, article.Respuesta });
    }

    /// <summary>
    /// Importa un documento (TXT/MD/PDF/DOCX), lo trocea y genera un embedding
    /// por fragmento. Cada fragmento se guarda como un KnowledgeBaseArticle.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<IActionResult> Import(IFormFile archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest("Debes adjuntar un archivo.");

        if (archivo.Length > MaxFileBytes)
            return BadRequest("El archivo supera el límite de 5 MB.");

        var fileName = archivo.FileName;
        if (!_ingestion.EsExtensionSoportada(fileName))
            return BadRequest("Extensión no soportada. Usa .txt, .md, .pdf o .docx.");

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await archivo.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        string texto;
        try
        {
            texto = _ingestion.ExtraerTexto(bytes, fileName);
        }
        catch (Exception ex)
        {
            return BadRequest($"No se pudo leer el archivo: {ex.Message}");
        }

        var chunks = _ingestion.Trocear(texto);
        if (chunks.Count == 0)
            return BadRequest("El documento no contiene texto utilizable.");

        var generados = 0;
        foreach (var chunk in chunks)
        {
            var embedding = await _gemini.GenerarEmbeddingAsync(chunk, ct);

            _coreDb.KnowledgeBaseArticles.Add(new KnowledgeBaseArticle
            {
                Id = Guid.NewGuid(),
                Pregunta = GenerarEtiqueta(chunk),
                Respuesta = chunk,
                Titulo = fileName,
                Embedding = new Vector(embedding)
            });
            generados++;
        }

        await _coreDb.SaveChangesAsync(ct);

        return Ok(new
        {
            mensaje = $"Se importaron {generados} fragmentos del documento '{fileName}'.",
            archivo = fileName,
            fragmentos = generados
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var article = await _coreDb.KnowledgeBaseArticles
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.Id, a.Pregunta, a.Respuesta, a.Titulo })
            .FirstOrDefaultAsync(ct);

        if (article is null)
            return NotFound();

        return Ok(article);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] KbArticleRequest request, CancellationToken ct)
    {
        var article = await _coreDb.KnowledgeBaseArticles.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (article is null)
            return NotFound();

        article.Pregunta = request.Pregunta.Trim();
        article.Respuesta = request.Respuesta.Trim();
        article.Embedding = new Vector(await _gemini.GenerarEmbeddingAsync($"{article.Pregunta}\n{article.Respuesta}", ct));

        await _coreDb.SaveChangesAsync(ct);

        return Ok(new { article.Id, article.Pregunta, article.Respuesta });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var article = await _coreDb.KnowledgeBaseArticles.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (article is null)
            return NotFound();

        _coreDb.KnowledgeBaseArticles.Remove(article);
        await _coreDb.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Etiqueta legible para un fragmento importado: un extracto corto del texto,
    /// sin saltos de línea, para que el artículo no aparezca sin "pregunta".
    /// </summary>
    private static string GenerarEtiqueta(string chunk)
    {
        var limpio = Regex.Replace(chunk, @"\s+", " ").Trim();
        return limpio.Length <= 120 ? limpio : limpio[..120] + "…";
    }
}
