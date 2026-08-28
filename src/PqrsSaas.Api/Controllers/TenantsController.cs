using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PqrsSaas.Domain.Entities;
using PqrsSaas.Infrastructure.Persistence;
using PqrsSaas.Infrastructure.Provisioning;
using PqrsSaas.Infrastructure.Services;

namespace PqrsSaas.Api.Controllers;

public record RegistrarTenantRequest(string Nombre, string DominioPermitido, string EmailAdministrador, string[]? DominiosPermitidos = null);

[ApiController]
[Route("api/v1/tenants")]
public class TenantsController : ControllerBase
{
    private readonly ControlDbContext _controlDb;
    private readonly TenantProvisioningService _provisioning;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;

    public TenantsController(ControlDbContext controlDb, TenantProvisioningService provisioning, IEmailSender emailSender, IConfiguration config)
    {
        _controlDb = controlDb;
        _provisioning = provisioning;
        _emailSender = emailSender;
        _config = config;
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

        var origenes = new List<string> { request.DominioPermitido };
        if (request.DominiosPermitidos is not null)
            origenes.AddRange(request.DominiosPermitidos.Where(d => !string.IsNullOrWhiteSpace(d)));
        origenes = origenes.Select(o => o.Trim().TrimEnd('/')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        tenant.Dominios = origenes.Select(o => new TenantDominio { Id = Guid.NewGuid(), Origen = o }).ToList();
        tenant.DominioPermitido = origenes[0];

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

        // Enviar las credenciales al administrador por correo. Si el SMTP no está
        // configurado o falla, se devuelven en la respuesta (flujo de desarrollo).
        var panelBase = _config["App:PanelBaseUrl"] ?? "http://localhost:8080";
        var enviado = await _emailSender.EnviarAsync(
            request.EmailAdministrador,
            "Tus credenciales de acceso · PQRS SaaS",
            CredentialEmailBuilder.Bienvenida(request.EmailAdministrador, request.EmailAdministrador, passwordAdmin, tenant.Slug, $"{panelBase}/agent/", tenant.Nombre),
            ct);

        var respuesta = new
        {
            tenant.Id,
            tenant.Nombre,
            tenant.Slug,
            tenant.ApiKeyWidget,
            credenciales = enviado
                ? (object)new
                {
                    emailAdministrador = request.EmailAdministrador,
                    enviadasPorCorreo = true,
                    aviso = "Las credenciales fueron enviadas al correo del administrador. Debe cambiarlas en su primer ingreso."
                }
                : (object)new
                {
                    emailAdministrador = request.EmailAdministrador,
                    password = passwordAdmin,
                    aviso = "SMTP no configurado: esta contraseña solo se muestra una vez. Guárdala ahora."
                }
        };

        return CreatedAtAction(nameof(Registrar), new { id = tenant.Id }, respuesta);
    }

    private static string GenerarSlug(string nombre)
    {
        var slug = nombre.Trim().ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, "[^a-z0-9]+", "_");
        return slug.Trim('_');
    }
}
