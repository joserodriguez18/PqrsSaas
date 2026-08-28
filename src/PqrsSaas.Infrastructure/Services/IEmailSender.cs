namespace PqrsSaas.Infrastructure.Services;

public interface IEmailSender
{
    /// <summary>Indica si hay un SMTP configurado (de lo contrario no se envía correo).</summary>
    bool Configurado { get; }

    /// <summary>Envía un correo HTML. Devuelve false si el envío falló o no hay SMTP.</summary>
    Task<bool> EnviarAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
