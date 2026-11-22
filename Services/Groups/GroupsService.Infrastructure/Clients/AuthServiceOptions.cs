namespace GroupsService.Infrastructure.Clients;

public class AuthServiceOptions
{
    public const string SectionName = "AuthService";

    public string BaseUrl { get; set; } = string.Empty;

    // GET /admin/users/{id} na Auth servisu trazi Admin JWT. Groups servis nema
    // sopstvenog korisnika da se uloguje kao admin, pa za sad koristi ovde upisan
    // STATICAN dugotrajan admin token (generisan rucno kroz Auth login, pa zalepljen
    // u user-secrets/konfiguraciju). Ovo je poznat tehnicki dug - prava service-to-service
    // autentikacija (client credentials, shared secret header, itd.) dolazi kasnije.
    public string ServiceAdminToken { get; set; } = string.Empty;
}