namespace CoursesService.API.Options;

public sealed class PricingOptions
{
    public const string Section = "Pricing";

    // Valuta u kojoj su unete cene u Directus-u (uplatnica i IPS QR rade u RSD).
    public string Currency { get; set; } = "RSD";
}

public sealed class GroupsServiceOptions
{
    public const string Section = "GroupsService";

    public string BaseUrl { get; set; } = "http://localhost:5003";
}
