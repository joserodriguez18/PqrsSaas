using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PqrsSaas.Domain.Entities;

namespace PqrsSaas.Infrastructure.Security;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerarToken(User user, Tenant tenant)
    {
        var secret = _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Falta Jwt:Secret en la configuración.");
        var issuer = _config["Jwt:Issuer"];
        var audience = _config["Jwt:Audience"];
        var minutos = int.TryParse(_config["Jwt:ExpirationMinutes"], out var m) ? m : 120;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Rol.ToString()),
            // Claim clave: permite a TenantResolutionMiddleware resolver la BD del
            // tenant en requests autenticados, sin depender del header X-Tenant-Api-Key.
            new Claim("tenantId", tenant.Id.ToString()),
            new Claim("tenantSlug", tenant.Slug)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutos),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerarTokenSuperAdmin(SuperAdmin superAdmin)
    {
        var secret = _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Falta Jwt:Secret en la configuración.");
        var issuer = _config["Jwt:Issuer"];
        var audience = _config["Jwt:Audience"];
        var minutos = int.TryParse(_config["Jwt:ExpirationMinutes"], out var m) ? m : 120;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, superAdmin.Id.ToString()),
            new Claim(ClaimTypes.Email, superAdmin.Email),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutos),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
