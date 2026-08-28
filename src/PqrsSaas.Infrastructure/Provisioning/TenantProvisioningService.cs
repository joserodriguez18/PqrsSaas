using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using PqrsSaas.Domain.Entities;
using PqrsSaas.Infrastructure.Persistence;
using PqrsSaas.Infrastructure.Security;

namespace PqrsSaas.Infrastructure.Provisioning;

/// <summary>
/// Módulo de mayor riesgo del proyecto (ver sección 3 del documento de arquitectura).
/// Construir y probar esto ANTES que cualquier otro módulo de negocio.
/// </summary>
public class TenantProvisioningService
{
    private readonly IConfiguration _config;
    private readonly ILogger<TenantProvisioningService> _logger;
    private readonly PasswordService _passwordService;

    public TenantProvisioningService(
        IConfiguration config,
        ILogger<TenantProvisioningService> logger,
        PasswordService passwordService)
    {
        _config = config;
        _logger = logger;
        _passwordService = passwordService;
    }

    /// <summary>
    /// Crea la BD física del tenant, aplica el esquema y siembra el usuario
    /// Administrador inicial (única forma de entrar al sistema la primera vez,
    /// ya que no hay endpoint de auto-registro de agentes en este MVP).
    /// </summary>
    /// <returns>La contraseña en texto plano del admin sembrado — solo se puede ver aquí.</returns>
    public async Task<string> ProvisionAsync(Tenant tenant, string emailAdministrador, CancellationToken ct = default)
    {
        var adminConnStr = _config.GetConnectionString("TenantAdmin")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:TenantAdmin en la configuración.");

        // 1. Crear la base de datos física en el servidor Postgres compartido.
        //    El usuario configurado en TenantAdmin debe tener permiso CREATEDB.
        await using (var adminConnection = new NpgsqlConnection(adminConnStr))
        {
            await adminConnection.OpenAsync(ct);

            // El nombre de la base ya viene validado/generado como slug seguro (sin espacios,
            // minúsculas, sin caracteres especiales) antes de llegar aquí — igual se usa
            // NpgsqlCommandBuilder para escapar el identificador por seguridad.
            var dbNameEscaped = new NpgsqlCommandBuilder().QuoteIdentifier(tenant.NombreBaseDatos);

            await using var createCmd = new NpgsqlCommand($"CREATE DATABASE {dbNameEscaped}", adminConnection);
            await createCmd.ExecuteNonQueryAsync(ct);
        }

        // 2. Construir el connection string de la nueva base a partir de la plantilla.
        var template = _config.GetConnectionString("TenantTemplate")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:TenantTemplate en la configuración.");
        var tenantConnStr = template.Replace("{db}", tenant.NombreBaseDatos);

        // 3. Habilitar pgvector y aplicar el esquema (migraciones EF Core) en la nueva base.
        await using (var tenantConnection = new NpgsqlConnection(tenantConnStr))
        {
            await tenantConnection.OpenAsync(ct);
            await using var extensionCmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", tenantConnection);
            await extensionCmd.ExecuteNonQueryAsync(ct);
        }

        var tempAccessor = new TenantConnectionAccessor { ConnectionString = tenantConnStr };
        await using var coreDb = new CoreDbContext(tempAccessor);

        // Requiere que exista al menos una migración generada con:
        //   dotnet ef migrations add InicialCore -c CoreDbContext -p PqrsSaas.Infrastructure -s PqrsSaas.Api
        await coreDb.Database.MigrateAsync(ct);

        // 4. Sembrar el usuario Administrador inicial de este tenant.
        var passwordGenerada = GenerarPasswordTemporal();
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = emailAdministrador,
            Rol = RolUsuario.Administrador,
            DebeCambiarPassword = true
        };
        admin.PasswordHash = _passwordService.Hash(admin, passwordGenerada);

        coreDb.Users.Add(admin);
        await coreDb.SaveChangesAsync(ct);

        _logger.LogInformation("Base de datos {Db} aprovisionada correctamente para el tenant {Tenant}",
            tenant.NombreBaseDatos, tenant.Nombre);

        return passwordGenerada;
    }

    private static string GenerarPasswordTemporal()
    {
        // Suficiente para un MVP académico — no es para producción real.
        return Guid.NewGuid().ToString("N")[..12];
    }
}
