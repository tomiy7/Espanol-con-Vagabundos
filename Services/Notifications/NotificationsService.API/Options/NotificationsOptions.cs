namespace NotificationsService.API.Options;

// Sta i kako servis salje (bez tajni - SMTP lozinka je u SmtpOptions iz env/secrets).
public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    // Jezik mejlova (vidi Templates/EmailTemplatesFactory). Za sada samo "sr".
    public string Language { get; set; } = "sr";

    // Adresa fronta - linkovi u mejlovima (npr. pozivnica) vode tamo.
    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";

    // Vremenska zona za datume u mejlovima (podaci u sistemu su u UTC).
    public string TimeZone { get; set; } = "Europe/Belgrade";

    public string SchoolName { get; set; } = "Español con Vagabundos";
}

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool UseSsl { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@espanolconvagabundos.local";
    public string FromName { get; set; } = "Español con Vagabundos";
}

// Adrese servisa od kojih se citaju mejl/ime korisnika i naziv kursa.
public sealed class AuthServiceOptions
{
    public const string SectionName = "AuthService";

    public string BaseUrl { get; set; } = string.Empty;

    // GET /auth/admin/users/{id} trazi admin token - isti privremeni pristup kao u Groups
    // (staticki token iz konfiguracije; prava service-to-service autentikacija kasnije).
    public string ServiceAdminToken { get; set; } = string.Empty;
}

public sealed class CoursesServiceOptions
{
    public const string SectionName = "CoursesService";

    public string BaseUrl { get; set; } = string.Empty;
}
