using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PqrsSaas.Infrastructure.Persistence;

public class CoreDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        var accessor = new TenantConnectionAccessor
        {
            ConnectionString =
                "Host=localhost;Database=pqrs_control;Username=postgres;Password=postgres"
        };

        return new CoreDbContext(accessor);
    }
}