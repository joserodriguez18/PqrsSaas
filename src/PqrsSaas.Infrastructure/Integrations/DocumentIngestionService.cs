using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace PqrsSaas.Infrastructure.Integrations;

/// <summary>
/// Extrae el texto de un archivo (TXT, PDF o DOCX) y lo divide en fragmentos
/// (chunks) con solape, listos para generar un embedding por chunk.
/// </summary>
public class DocumentIngestionService
{
    // Tamaño de chunk y solape en caracteres.
    private const int ChunkSize = 900;
    private const int ChunkOverlap = 150;
    private const int MaxChunks = 150;

    private static readonly HashSet<string> Soportados = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".pdf", ".docx"
    };

    public bool EsExtensionSoportada(string fileName)
    {
        return Soportados.Contains(System.IO.Path.GetExtension(fileName));
    }

    /// <summary>
    /// Extrae el texto plano del archivo según su extensión.
    /// </summary>
    public string ExtraerTexto(byte[] bytes, string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

        return ext switch
        {
            ".txt" or ".md" => Encoding.UTF8.GetString(bytes),
            ".pdf" => ExtraerTextoPdf(bytes),
            ".docx" => ExtraerTextoDocx(bytes),
            _ => throw new InvalidOperationException($"Extensión no soportada: {ext}")
        };
    }

    /// <summary>
    /// Divide el texto en chunks con solape, respetando límites de párrafo.
    /// </summary>
    public IReadOnlyList<string> Trocear(string texto)
    {
        var limpio = Normalizar(texto);
        if (limpio.Length == 0)
            return Array.Empty<string>();

        var chunks = new List<string>();
        var buffer = new StringBuilder();

        // Separar por párrafos y acumular hasta llegar al tamaño objetivo.
        foreach (var parrafo in SplitParrafos(limpio))
        {
            if (buffer.Length + parrafo.Length > ChunkSize && buffer.Length > 0)
            {
                if (buffer.Length >= ChunkOverlap)
                {
                    chunks.Add(buffer.ToString().Trim());
                    // Dejar un solape del final del buffer para mantener contexto.
                    var restante = buffer.ToString();
                    buffer.Clear();
                    buffer.Append(restante, restante.Length - Math.Min(ChunkOverlap, restante.Length), Math.Min(ChunkOverlap, restante.Length));
                    buffer.Append('\n');
                }
                else
                {
                    chunks.Add(buffer.ToString().Trim());
                    buffer.Clear();
                }
            }

            buffer.Append(parrafo);
            buffer.Append('\n');

            // Un párrafo más grande que el tamaño objetivo se parte solo.
            while (buffer.Length > ChunkSize)
            {
                var trozo = buffer.ToString(0, ChunkSize);
                chunks.Add(trozo.Trim());
                buffer.Remove(0, ChunkSize - ChunkOverlap);
                buffer.Insert(0, '\n');
            }
        }

        if (buffer.ToString().Trim().Length > 0)
            chunks.Add(buffer.ToString().Trim());

        return chunks.Take(MaxChunks).ToList();
    }

    private static IEnumerable<string> SplitParrafos(string texto)
    {
        return texto
            .Split('\n')
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);
    }

    private static string Normalizar(string texto)
    {
        var t = Regex.Replace(texto, @"\r\n?", "\n");
        t = Regex.Replace(t, @"[ \t]+", " ");
        t = Regex.Replace(t, @"\n{3,}", "\n\n");
        return t.Trim();
    }

    private static string ExtraerTextoPdf(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var doc = PdfDocument.Open(stream);

        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);

        return sb.ToString();
    }

    private static string ExtraerTextoDocx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        var entry = zip.GetEntry("word/document.xml")
            ?? throw new InvalidOperationException("El .docx no contiene word/document.xml.");

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var xml = reader.ReadToEnd();

        // Quitar etiquetas XML y desescapar entidades básicas.
        var texto = Regex.Replace(xml, "<w:p[ >]", "\n");
        texto = Regex.Replace(texto, "<[^>]+>", string.Empty);
        texto = System.Net.WebUtility.HtmlDecode(texto);

        return texto;
    }
}
