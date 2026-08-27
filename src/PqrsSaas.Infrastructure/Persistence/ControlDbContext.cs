using Microsoft.EntityFrameworkCore;
using PqrsSaas.Domain.Entities;

namespace PqrsSaas.Infrastructure.Persistence;

public class ControlDbContext : DbContext
{
    public ControlDbContext(DbContextOptions<ControlDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantConfiguracion> TenantConfiguraciones => Set<TenantConfiguracion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasIndex(t => t.Slug).IsUnique();
            e.HasIndex(t => t.ApiKeyWidget).IsUnique();
            e.Property(t => t.EstadoProvisionamiento).HasConversion<string>();
        });

        modelBuilder.Entity<TenantConfiguracion>(e =>
        {
            e.HasOne(c => c.Tenant)
             .WithOne(t => t.Configuracion)
             .HasForeignKey<TenantConfiguracion>(c => c.TenantId);
        });
    }
}
