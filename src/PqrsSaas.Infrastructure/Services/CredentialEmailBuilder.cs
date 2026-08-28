namespace PqrsSaas.Infrastructure.Services;

public static class CredentialEmailBuilder
{
    /// <summary>
    /// Construye el correo HTML de bienvenida con las credenciales temporales,
    /// el slug del tenant y el enlace al panel de agentes.
    /// </summary>
    public static string Bienvenida(string nombre, string email, string password, string? tenantSlug = null, string? panelUrl = null, string? tenantNombre = null)
    {
        var contexto = tenantNombre is null
            ? "tu cuenta en la plataforma PQRS SaaS"
            : $"la cuenta del tenant <strong>{System.Net.WebUtility.HtmlEncode(tenantNombre)}</strong> en la plataforma PQRS SaaS";

        var slugBlock = string.IsNullOrWhiteSpace(tenantSlug)
            ? string.Empty
            : $@"
      <p style=""margin:0 0 8px;""><strong>Tenant (slug):</strong> <code style=""background:#e2e8f0; padding:2px 6px; border-radius:4px; font-size:14px;"">{System.Net.WebUtility.HtmlEncode(tenantSlug)}</code></p>";

        var panelBlock = string.IsNullOrWhiteSpace(panelUrl)
            ? string.Empty
            : $@"
    <p style=""color:#475569;"">Para ingresar, usa este enlace:</p>
    <p style=""text-align:center; margin:16px 0;""><a href=""{System.Net.WebUtility.HtmlEncode(panelUrl)}"" style=""background:#1e3a8a; color:#ffffff; padding:10px 20px; border-radius:8px; text-decoration:none; font-weight:600;"">Ir al panel</a></p>";

        return $@"
<!DOCTYPE html>
<html>
<body style=""font-family: Arial, Helvetica, sans-serif; background:#f8fafc; padding:24px;"">
  <div style=""max-width:520px; margin:auto; background:#ffffff; border:1px solid #e2e8f0; border-radius:12px; padding:24px;"">
    <h2 style=""margin:0 0 8px; color:#0f172a;"">¡Bienvenido, {System.Net.WebUtility.HtmlEncode(nombre)}!</h2>
    <p style=""color:#475569;"">Se creó {contexto}.</p>
    <p style=""color:#475569;"">Estas son tus credenciales temporales:</p>
    <div style=""background:#f1f5f9; border:1px solid #e2e8f0; border-radius:8px; padding:16px; margin:16px 0;"">
      {slugBlock}
      <p style=""margin:0 0 8px;""><strong>Correo:</strong> {System.Net.WebUtility.HtmlEncode(email)}</p>
      <p style=""margin:0;""><strong>Contraseña temporal:</strong> <code style=""background:#e2e8f0; padding:2px 6px; border-radius:4px; font-size:14px;"">{System.Net.WebUtility.HtmlEncode(password)}</code></p>
    </div>
    {panelBlock}
    <p style=""color:#b45309; font-weight:600;"">Importante: cambia esta contraseña en tu primer ingreso.</p>
    <p style=""color:#475569;"">Si no solicitaste esta cuenta, puedes ignorar este correo.</p>
  </div>
</body>
</html>";
    }
}
