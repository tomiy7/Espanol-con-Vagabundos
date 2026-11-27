namespace GroupsService.Application.Options;

public class EnrollmentOptions
{
    public const string SectionName = "Enrollment";

    public int GroupFormationWindowDays { get; set; } = 7;
    public int PairInvitationExpiryDays { get; set; } = 5;
    public int PartnerInvitationMaxAttempts { get; set; } = 2;
    
    // Koliko se lekcija otvara nedeljno (od starta grupe, bez zamrznutog vremena).
    public int LessonsPerWeek { get; set; } = 3;

    // Najduze trajanje zamrzavanja; posle toga profesor/admin odlucuje (odmrznuti
    // ili otkazati). Pozadinski posao koji ovo proverava je planiran.
    public int MaxFreezeDays { get; set; } = 30;

    // Koliko dana posle zavrsetka kursa ucenik i dalje vidi materijal (front prikazuje
    // "Sadrzaj kursa mozete da vidite do ..."). 0 = pristup prestaje odmah.
    public int CompletedAccessDays { get; set; } = 30;

    // Bazni URL fronta na koji se lepe /invitations/{token}/accept|decline linkovi
    // u mejlu. Ako frontend hoce svoju landing stranicu koja tek onda zove API,
    // ovde ide frontend URL umesto direktnog API URL-a - vidi SETUP.md.
    public string PublicBaseUrl { get; set; } = "http://localhost:5003";
}