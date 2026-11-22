namespace GroupsService.Smoke.Tests;

// Smoke testovi gadjaju vec pokrenut Groups API (docker compose, staging, posle deploy-a).
// Sve dolazi iz env varijabli; bez GROUPS_SMOKE_BASE_URL testovi se preskacu.
//
// Tokeni, jedan od dva nacina:
//  - lokalno/CI: GROUPS_SMOKE_JWT_SECRET - testovi sami potpisuju token istim tajnim
//    kljucem kao Auth (NIKAD ne koristiti za staging/produkciju);
//  - pravo okruzenje: GROUPS_SMOKE_AUTH_URL + nalozi osoblja
//    (GROUPS_SMOKE_ADMIN_LOGIN/_PASSWORD, GROUPS_SMOKE_PROFESSOR_LOGIN/_PASSWORD);
//    ucenici se registruju kroz Auth kao pravi korisnici.
public static class SmokeSettings
{
    public static string? BaseUrl => Get("GROUPS_SMOKE_BASE_URL")?.TrimEnd('/');
    public static string? JwtSecret => Get("GROUPS_SMOKE_JWT_SECRET");
    public static string? AuthUrl => Get("GROUPS_SMOKE_AUTH_URL")?.TrimEnd('/');
    public static string? AdminLogin => Get("GROUPS_SMOKE_ADMIN_LOGIN");
    public static string? AdminPassword => Get("GROUPS_SMOKE_ADMIN_PASSWORD");
    public static string? ProfessorLogin => Get("GROUPS_SMOKE_PROFESSOR_LOGIN");
    public static string? ProfessorPassword => Get("GROUPS_SMOKE_PROFESSOR_PASSWORD");

    public static bool HasApi => BaseUrl is not null;

    public static bool HasTokens =>
        JwtSecret is not null
        || (AuthUrl is not null && AdminLogin is not null && AdminPassword is not null
            && ProfessorLogin is not null && ProfessorPassword is not null);

    private static string? Get(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

// Testovi koji traze samo pokrenut API (bez korisnika).
public sealed class SmokeFactAttribute : FactAttribute
{
    public SmokeFactAttribute()
    {
        if (!SmokeSettings.HasApi)
            Skip = "Set GROUPS_SMOKE_BASE_URL to run smoke tests against a running Groups API.";
    }
}

// Testovi koji traze i korisnike (token preko tajnog kljuca ili preko Auth-a).
public sealed class SmokeFlowFactAttribute : FactAttribute
{
    public SmokeFlowFactAttribute()
    {
        if (!SmokeSettings.HasApi)
            Skip = "Set GROUPS_SMOKE_BASE_URL to run smoke tests against a running Groups API.";
        else if (!SmokeSettings.HasTokens)
            Skip = "Set GROUPS_SMOKE_JWT_SECRET (local/CI) or GROUPS_SMOKE_AUTH_URL with staff credentials.";
    }
}
