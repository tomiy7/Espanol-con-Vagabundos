using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using NotificationsService.API.Options;
using NotificationsService.API.Templates;

namespace NotificationsService.API.Email;

public interface IEmailSender
{
    Task SendAsync(string toAddress, string? toName, EmailMessage message, CancellationToken cancellationToken);
}

// Slanje preko SMTP-a (lokalno Mailpit, u produkciji bilo koji SMTP provajder).
// Greska se ne hvata: potrosac poruka ponavlja, pa dead-letter.
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string toAddress, string? toName, EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(toName ?? string.Empty, toAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.Html, TextBody = message.Text }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 15_000 };
        var security = SecurityFor(_options);
        await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken);
        if (!string.IsNullOrEmpty(_options.UserName))
            await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, cancellationToken);
        await client.SendAsync(FormatOptions.Default, new SendRequest(mime), cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    // Port 465 trazi TLS odmah na pocetku veze, ostali portovi (587) prelaze na TLS posle
    // pozdrava (STARTTLS). Bez SSL-a samo lokalno (Mailpit).
    public static SecureSocketOptions SecurityFor(SmtpOptions options) => options switch
    {
        { UseSsl: false } => SecureSocketOptions.None,
        { Port: 465 } => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls
    };
}
