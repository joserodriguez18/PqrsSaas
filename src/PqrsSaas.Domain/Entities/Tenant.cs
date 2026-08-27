namespace PqrsSaas.Domain.Entities;

public enum EstadoProvisionamiento
{
    Pendiente,
    Completado,
    Error
}

public class Tenant
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string DominioPermitido { get; set; } = default!;
    public string ApiKeyWidget { get; set; } = default!;
    public string NombreBaseDatos { get; set; } = default!;
    public EstadoProvisionamiento EstadoProvisionamiento { get; set; } = EstadoProvisionamiento.Pendiente;
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public TenantConfiguracion? Configuracion { get; set; }
}

public class TenantConfiguracion
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    public string? ColorPrimario { get; set; }
    public string? Logo { get; set; }
    public double UmbralSimilitudRAG { get; set; } = 0.75;
    public int? LimiteTicketsMes { get; set; }
}
