using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PqrsSaas.Domain.Entities;
using PqrsSaas.Infrastructure.Persistence;
using PqrsSaas.Infrastructure.Provisioning;

namespace PqrsSaas.Api.Controllers;

public record RegistrarTenantRequest(string Nombre, string DominioPermitido, string EmailAdministrador);

[ApiController]
[Route("api/v1/tenants")]
public class TenantsController : ControllerBase
{
    private readonly ControlDbContext _controlDb;
    private readonly TenantProvisioningService _provisioning;

    public TenantsController(ControlDbContext controlDb, TenantProvisioningService provisioning)
    {
        _controlDb = controlDb;
        _provisioning = provisioning;
    }

    [HttpPost("registro")]
    public async Task<IActionResult> Registrar([FromBody] RegistrarTenantRequest request, CancellationToken ct)
    {
        var slug = GenerarSlug(request.Nombre);

        if (await _controlDb.Tenants.AnyAsync(t => t.Slug == slug, ct))
            return Conflict("Ya existe un tenant con un nombre que genera el mismo slug.");

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Nombre = request.Nombre,
            Slug = slug,
            DominioPermitido = request.DominioPermitido,
            ApiKeyWidget = Guid.NewGuid().ToString("N"),
            NombreBaseDatos = $"pqrs_tenant_{slug}",
            EstadoProvisionamiento = EstadoProvisionamiento.Pendiente
        };

        _controlDb.Tenants.Add(tenant);
        await _controlDb.SaveChangesAsync(ct);

        string passwordAdmin;
        try
        {
            passwordAdmin = await _provisioning.ProvisionAsync(tenant, request.EmailAdministrador, ct);
            tenant.EstadoProvisionamiento = EstadoProvisionamiento.Completado;
        }
        catch (Exception ex)
        {
            tenant.EstadoProvisionamiento = EstadoProvisionamiento.Error;
            await _controlDb.SaveChangesAsync(ct);

            // TODO (módulo 2, endurecer): intentar DROP DATABASE de limpieza si ya llegó a crearse.
            return Problem(
                title: "No se pudo aprovisionar la base de datos del tenant.",
                detail: ex.Message,
                statusCode: 500);
        }

        await _controlDb.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Registrar), new { id = tenant.Id }, new
        {
            tenant.Id,
            tenant.Nombre,
            tenant.Slug,
            tenant.ApiKeyWidget,
            credencialesAdmin = new
            {
                request.EmailAdministrador,
                password = passwordAdmin,
                aviso = "Esta contraseña solo se muestra una vez. Guárdala ahora."
            }
        });
    }

    private static string GenerarSlug(string nombre)
    {
        var slug = nombre.Trim().ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, "[^a-z0-9]+", "_");
        return slug.Trim('_');
    }
}
