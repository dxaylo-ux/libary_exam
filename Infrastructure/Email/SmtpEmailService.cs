using System.Net;
using System.Net.Mail;
using Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

public class SmtpEmailService(IOptions<EmailSettings> options) : IEmailService
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var settings = options.Value;

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(settings.UserName))
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);

        using var message = new MailMessage(settings.From, to, subject, body);
        await client.SendMailAsync(message, ct);
    }
}
