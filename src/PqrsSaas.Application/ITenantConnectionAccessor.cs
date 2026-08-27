namespace PqrsSaas.Application;

/// <summary>
/// Servicio scoped (uno por request) que guarda el connection string
/// de la base de datos del tenant activo, resuelto por el middleware
/// de resolución de tenant. CoreDbContext lo consulta en OnConfiguring.
/// </summary>
public interface ITenantConnectionAccessor
{
    string? ConnectionString { get; set; }
}
