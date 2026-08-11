using MailKit.Net.Smtp;
using MimeKit;


namespace MotoParts.Api.Services;

public interface IEmailService
{
    // Описываем наш новый метод
    Task SendPasswordResetLinkAsync(string toEmail, string resetLink);

    // Если у вас были другие методы отправки писем, добавьте их сюда тоже
}

/// <summary>
/// Отправка почты через SMTP (настройки в appsettings: Smtp:Host/Port/User/Password/From).
/// Если SMTP не настроен, письмо пишется в лог — удобно для разработки.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendPasswordResetLinkAsync(string toEmail, string resetLink)
    {
        // Читаем настройки из appsettings.json
        var host = _configuration["SmtpSettings:Host"];
        var port = int.Parse(_configuration["SmtpSettings:Port"] ?? "465");
        var username = _configuration["SmtpSettings:Username"];
        var password = _configuration["SmtpSettings:Password"];
        var fromEmail = _configuration["SmtpSettings:FromEmail"];

        // Создаем структуру письма
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("DonorGarage", fromEmail));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = "Восстановление пароля";

        // Тело письма (можно HTML-разметку)
        message.Body = new TextPart("html")
        {
            Text = $@"
                    <h3>Восстановление пароля</h3>
                    <p>Для сброса пароля в магазине DonorGarage перейдите по ссылке ниже:</p>
                    <p><a href='{resetLink}' style='padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>Сбросить пароль</a></p>
                    <p>Если вы не запрашивали сброс, просто проигнорируйте это письмо.</p>"
        };

        // Подключаемся к серверу Яндекса и отправляем
        using (var client = new SmtpClient())
        {
            // Для порта 465 используем true (useSsl)
            await client.ConnectAsync(host, port, true);

            // Аутентификация с помощью пароля приложения
            await client.AuthenticateAsync(username, password);

            // Отправка
            await client.SendAsync(message);

            // Отключение
            await client.DisconnectAsync(true);
        }
    }
}
