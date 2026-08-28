using Pgvector;
namespace PqrsSaas.Domain.Entities;

public enum RolUsuario
{
    Agente,
    Administrador
}

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public RolUsuario Rol { get; set; } = RolUsuario.Agente;
    public bool Activo { get; set; } = true;
    public bool DebeCambiarPassword { get; set; } = false;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}

public class KnowledgeBaseArticle
{
    public Guid Id { get; set; }
    public string Pregunta { get; set; } = default!;
    public string Respuesta { get; set; } = default!;

    // Fuente (nombre del documento) cuando el artículo proviene de una ingesta
    // automática de archivos; null para FAQs creadas manualmente.
    public string? Titulo { get; set; }

    // Generado con gemini-embedding-001, output_dimensionality=768
    public Vector Embedding { get; set; } = default!;

}

public enum TipoPqrs
{
    Peticion,
    Queja,
    Reclamo,
    Sugerencia
}

public enum EstadoTicket
{
    Pendiente,
    EnProceso,
    Resuelto
}

public enum PrioridadTicket
{
    Baja,
    Media,
    Alta
}

public enum SentimientoTicket
{
    Positivo,
    Neutro,
    Negativo
}

public class Ticket
{
    public Guid Id { get; set; }
    public string NumeroRadicado { get; set; } = default!;
    public string ClienteNombre { get; set; } = default!;
    public string ClienteCorreo { get; set; } = default!;
    public string Asunto { get; set; } = default!;
    public string Descripcion { get; set; } = default!;

    // Campos asignados por el módulo de IA (triaje)
    public TipoPqrs? Tipo { get; set; }
    public PrioridadTicket? Prioridad { get; set; }
    public SentimientoTicket? Sentimiento { get; set; }
    public string? Resumen { get; set; }

    public EstadoTicket Estado { get; set; } = EstadoTicket.Pendiente;
    public bool ResueltoPorRAG { get; set; } = false;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
