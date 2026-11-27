using NotificationsService.API.Options;

namespace NotificationsService.API.Templates;

// Mejlovi na srpskom (latinica). Glagoli su u obliku koji ne zavisi od roda primaoca.
public sealed class SerbianEmailTemplates : IEmailTemplates
{
    private static readonly string[] Days =
        ["nedelja", "ponedeljak", "utorak", "sreda", "četvrtak", "petak", "subota"];

    private readonly NotificationsOptions _options;
    private readonly TimeZoneInfo _timeZone;

    public SerbianEmailTemplates(NotificationsOptions options)
    {
        _options = options;
        _timeZone = TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZone, out var zone) ? zone : TimeZoneInfo.Utc;
    }

    private EmailBuilder Mail(string? name) =>
        new EmailBuilder(_options.SchoolName).Paragraph(string.IsNullOrWhiteSpace(name) ? "Zdravo," : $"Zdravo {name},");

    private static string Course(string? course) => string.IsNullOrWhiteSpace(course) ? "izabrani kurs" : $"kurs „{course}“";

    private string Date(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);
        return $"{local.Day}. {local.Month}. {local.Year}.";
    }

    private string DateTimeText(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);
        return $"{Date(utc)} u {local:HH\\:mm}";
    }

    public EmailMessage PartnerInvited(string? inviteeName, string? inviterName, string? course, string link, DateTime expiresAtUtc)
    {
        var who = string.IsNullOrWhiteSpace(inviterName) ? "Jedan polaznik" : inviterName;
        return Mail(inviteeName)
            .Paragraph($"{who} te poziva da zajedno pohađate {Course(course)} u paru.")
            .Paragraph($"Poziv važi do {DateTimeText(expiresAtUtc)}. Ako ne želiš da se upišeš, poziv možeš da odbiješ na istoj stranici.")
            .Button("Pogledaj poziv", link)
            .Build("Poziv za učenje u paru");
    }

    public EmailMessage InvitationAccepted(string? name, string? partnerName, string? course)
    {
        var partner = string.IsNullOrWhiteSpace(partnerName) ? "Tvoj partner" : partnerName;
        return Mail(name)
            .Paragraph($"{partner} je prihvatio/la tvoj poziv. Vaš par za {Course(course)} je formiran.")
            .Paragraph("Profesor će uskoro odrediti termine časova, a mi ćemo ti javiti kada počinjete.")
            .Build("Poziv je prihvaćen");
    }

    public EmailMessage InvitationDeclined(string? name, string? partnerName, string? course)
    {
        var partner = string.IsNullOrWhiteSpace(partnerName) ? "Osoba koju si pozvao/la" : partnerName;
        return Mail(name)
            .Paragraph($"{partner} nije prihvatio/la poziv za {Course(course)}.")
            .Paragraph("Možeš da pozoveš nekog drugog ili da se upišeš u grupu ili na individualne časove.")
            .Build("Poziv nije prihvaćen");
    }

    public EmailMessage EnrollmentCreated(string? name, string? course, string enrollmentType, bool alreadyInGroup)
    {
        var text = enrollmentType switch
        {
            "Individual" => $"Primili smo tvoju prijavu za individualne časove — {Course(course)}. Profesor će potvrditi termine, pa ti javljamo kada počinješ.",
            "Group" => $"Upisan/a si u grupu za {Course(course)}. Čim se grupa popuni, javićemo ti.",
            "Pair" when alreadyInGroup => $"Prihvaćen je poziv za učenje u paru — {Course(course)}. Profesor će odrediti termine časova.",
            "Pair" => $"Poziv za učenje u paru je poslat — {Course(course)}. Javićemo ti kada partner odgovori.",
            _ => $"Primili smo tvoju prijavu za {Course(course)}."
        };
        return Mail(name).Paragraph(text).Build("Prijava je primljena");
    }

    public EmailMessage GroupReady(string? name, string? course) =>
        Mail(name)
            .Paragraph($"Tvoja grupa za {Course(course)} je popunjena.")
            .Paragraph("Uskoro počinjemo — javićemo ti tačan početak i raspored časova.")
            .Build("Grupa je popunjena");

    public EmailMessage ReturnedToSearching(string? name, string? course) =>
        Mail(name)
            .Paragraph($"Jedan polaznik je odustao, pa tvoja grupa za {Course(course)} više nije popunjena.")
            .Paragraph("Tvoje mesto je sačuvano. Javićemo ti čim se grupa ponovo popuni.")
            .Build("Grupa čeka novog člana");

    public EmailMessage GroupStarted(string? name, string? course, IReadOnlyList<WeeklySlot> schedule, DateTime unlockStartsAtUtc)
    {
        var mail = Mail(name).Paragraph($"Počinje {Course(course)}!");
        if (schedule.Count > 0)
            mail.Paragraph("Raspored časova:\n" + string.Join("\n",
                schedule.Select(s => $"• {Days[(int)s.Day]} u {s.Time:HH\\:mm}")));
        return mail
            .Paragraph($"Lekcije se otvaraju od {Date(unlockStartsAtUtc)}, tri nedeljno.")
            .Build("Kurs počinje");
    }

    public EmailMessage GroupDissolved(string? name, string? course, string groupType)
    {
        var mail = Mail(name);
        if (groupType == "Pair")
            mail.Paragraph($"Tvoj partner je odustao, pa par za {Course(course)} više ne postoji.")
                .Paragraph("Tvoja prijava je i dalje aktivna — možeš da pozoveš nekog drugog.");
        else
            mail.Paragraph($"Grupa za {Course(course)} je rasformirana pre početka.")
                .Paragraph("Tvoja prijava je i dalje aktivna — javićemo ti sledeći korak.");
        return mail.Build("Promena u tvojoj prijavi");
    }

    public EmailMessage FirstLessonRescheduled(string? name, string? course, DateTime oldAtUtc, DateTime newAtUtc) =>
        Mail(name)
            .Paragraph($"Prvi čas za {Course(course)} je pomeren.")
            .Paragraph($"Novi termin: {DateTimeText(newAtUtc)} (umesto {DateTimeText(oldAtUtc)}).")
            .Paragraph("Ako novi termin ne odgovara, javi nam se odgovorom na ovaj mejl.")
            .Build("Prvi čas je pomeren");

    public EmailMessage EnrollmentFrozen(string? name, string? course) =>
        Mail(name)
            .Paragraph($"Tvoj upis za {Course(course)} je zamrznut. Mesto ti je sačuvano, a vreme za lekcije stoji dok je upis zamrznut.")
            .Build("Upis je zamrznut");

    public EmailMessage EnrollmentUnfrozen(string? name, string? course) =>
        Mail(name)
            .Paragraph($"Tvoj upis za {Course(course)} je ponovo aktivan. Nastavljamo tamo gde smo stali!")
            .Build("Upis je ponovo aktivan");

    public EmailMessage EnrollmentCancelled(string? name, string? course) =>
        Mail(name)
            .Paragraph($"Tvoj upis za {Course(course)} je otkazan.")
            .Paragraph("Ako je u pitanju greška, javi nam se. Uvek možeš ponovo da se upišeš.")
            .Build("Upis je otkazan");

    public EmailMessage EnrollmentCompleted(string? name, string? course) =>
        Mail(name)
            .Paragraph($"Čestitamo — završen je {Course(course)}! ¡Felicidades!")
            .Paragraph("Hvala što učiš sa nama. Vidimo se na sledećem kursu.")
            .Build("Kurs je završen");
}
