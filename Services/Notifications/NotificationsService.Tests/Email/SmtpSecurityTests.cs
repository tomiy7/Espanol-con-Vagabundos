using MailKit.Security;
using NotificationsService.API.Email;
using NotificationsService.API.Options;

namespace NotificationsService.Tests.Email;

public class SmtpSecurityTests
{
    [Theory]
    [InlineData(false, 1025, SecureSocketOptions.None)]        // Mailpit lokalno
    [InlineData(true, 587, SecureSocketOptions.StartTls)]      // Gmail, Brevo, SES...
    [InlineData(true, 465, SecureSocketOptions.SslOnConnect)]  // provajderi samo sa implicitnim TLS-om
    [InlineData(true, 2525, SecureSocketOptions.StartTls)]
    public void Tls_mode_follows_the_port(bool useSsl, int port, SecureSocketOptions expected)
    {
        Assert.Equal(expected, SmtpEmailSender.SecurityFor(new SmtpOptions { UseSsl = useSsl, Port = port }));
    }
}
