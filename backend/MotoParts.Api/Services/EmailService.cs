using System.Net;
using System.Net.Mail;

namespace MotoParts.Api.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody);
}

/// <summary>
/// Отправка почты через SMTP (настройки в appsettings: Smtp:Host/Port/User/Password/From).
/// Если SMTP не настроен, письмо пишется в лог — удобно для разработки.
/// </summary>
public class EmailService(IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host = config["Smtp:Host"];
        if (string.IsNullOrEmpty(host))
        {
            logger.LogInformation("SMTP не настроен. Письмо для {To}: {Subject}\n{Body}", to, subject, htmlBody);
            return;
        }

        using var client = new SmtpClient(host, int.Parse(config["Smtp:Port"] ?? "587"))
        {
            EnableSsl = bool.Parse(config["Smtp:EnableSsl"] ?? "true"),
            Credentials = new NetworkCredential(config["Smtp:User"], config["Smtp:Password"]),
        };

        using var message = new MailMessage(config["Smtp:From"] ?? config["Smtp:User"]!, to, subject, htmlBody)
        {
            IsBodyHtml = true,
        };

        await client.SendMailAsync(message);
    }
}
