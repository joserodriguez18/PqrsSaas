using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PqrsSaas.Infrastructure.Services;

/// <summary>
/// Envía correos vía SMTP. La configuración sale de la sección `Smtp`
/// (env SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASS, SMTP_FROM, SMTP_FROM_NAME, SMTP_SSL).
/// Si no hay host configurado, `Configurado` es false y no se envía nada.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly string? _host;
    private readonly int _port;
    private readonly string? _user;
    private readonly string? _pass;
    private readonly bool _ssl;
    private readonly string? _from;
    private readonly string? _fromName;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;
        _host = config["Smtp:Host"];
        _port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        _user = config["Smtp:Username"];
        _pass = config["Smtp:Password"];
        _ssl = !string.Equals(config["Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);
        _from = config["Smtp:From"] ?? "no-reply@pqrs.local";
        _fromName = config["Smtp:FromName"] ?? "PQRS SaaS";
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_host);

    public async Task<bool> EnviarAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!Configurado)
        {
            _logger.LogWarning("SMTP no configurado; no se envió el correo a {To}.", to);
            return false;
        }

        try
        {
            using var client = new SmtpClient(_host, _port)
            {
                EnableSsl = _ssl,
                Credentials = string.IsNullOrEmpty(_user)
                    ? null
                    : new NetworkCredential(_user, _pass)
            };

            var message = new MailMessage
            {
                From = new MailAddress(_from, _fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Correo enviado a {To}: {Subject}", to, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando correo a {To}.", to);
            return false;
        }
    }
}
