using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using PqrsSaas.Application;
using PqrsSaas.Domain.Entities;

namespace PqrsSaas.Infrastructure.Persistence;

/// <summary>
/// A diferencia de ControlDbContext, este NO recibe su connection string
/// por DbContextOptions en el registro de DI (no se conoce en Program.cs,
/// porque cambia por tenant). En vez de eso, lee el connection string
/// resuelto por el middleware de tenant en OnConfiguring, en cada request.
/// </summary>
public class CoreDbContext : DbContext
{
    private readonly ITenantConnectionAccessor _tenantConnectionAccessor;

    public CoreDbContext(ITenantConnectionAccessor tenantConnectionAccessor)
    {
        _tenantConnectionAccessor = tenantConnectionAccessor;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles => Set<KnowledgeBaseArticle>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (string.IsNullOrEmpty(_tenantConnectionAccessor.ConnectionString))
        {
            throw new InvalidOperationException(
                "No se ha resuelto la conexión del tenant para este request. " +
                "¿Falta el header X-Tenant-Api-Key o el middleware de resolución de tenant?");
        }

        optionsBuilder.UseNpgsql(
            _tenantConnectionAccessor.ConnectionString,
            o => o.UseVector());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KnowledgeBaseArticle>(e =>
        {
            e.Property(a => a.Embedding).HasColumnType("vector(768)");
        });

        modelBuilder.Entity<User>(e => e.Property(u => u.Rol).HasConversion<string>());

        modelBuilder.Entity<Ticket>(e =>
        {
            e.Property(t => t.Tipo).HasConversion<string>();
            e.Property(t => t.Estado).HasConversion<string>();
            e.Property(t => t.Prioridad).HasConversion<string>();
            e.Property(t => t.Sentimiento).HasConversion<string>();
            e.HasIndex(t => t.Estado);
            e.HasIndex(t => t.Prioridad);
        });
    }
}
