using PqrsSaas.Application;

namespace PqrsSaas.Infrastructure.Persistence;

public class TenantConnectionAccessor : ITenantConnectionAccessor
{
    public string? ConnectionString { get; set; }
}
