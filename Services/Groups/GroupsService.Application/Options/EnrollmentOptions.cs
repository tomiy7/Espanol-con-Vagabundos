namespace GroupsService.Application.Options;

public class EnrollmentOptions
{
    public const string SectionName = "Enrollment";

    public int GroupFormationWindowDays { get; set; } = 7;
    public int PairInvitationExpiryDays { get; set; } = 5;
    public int PartnerInvitationMaxAttempts { get; set; } = 2;

    // Bazni URL fronta na koji se lepe /invitations/{token}/accept|decline linkovi
    // u mejlu. Ako frontend hoce svoju landing stranicu koja tek onda zove API,
    // ovde ide frontend URL umesto direktnog API URL-a - vidi SETUP.md.
    public string PublicBaseUrl { get; set; } = "http://localhost:5003";
}