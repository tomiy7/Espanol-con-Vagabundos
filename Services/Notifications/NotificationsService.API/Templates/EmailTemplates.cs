using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using NotificationsService.API.Options;

namespace NotificationsService.API.Templates;

public sealed record EmailMessage(string Subject, string Html, string Text);

public sealed record WeeklySlot(DayOfWeek Day, TimeOnly Time);

// Sadrzaj svih mejlova na jednom mestu. Novi jezik = nova implementacija ovog interfejsa
// i jedna linija u EmailTemplatesFactory; handleri se ne menjaju.
// Imena/kurs mogu da budu prazni (Auth/Courses nisu dali podatak) - sablon tada koristi
// neutralan tekst.
public interface IEmailTemplates
{
    EmailMessage PartnerInvited(string? inviteeName, string? inviterName, string? course, string link, DateTime expiresAtUtc);
    EmailMessage InvitationAccepted(string? name, string? partnerName, string? course);
    EmailMessage InvitationDeclined(string? name, string? partnerName, string? course);
    EmailMessage EnrollmentCreated(string? name, string? course, string enrollmentType, bool alreadyInGroup);
    EmailMessage GroupReady(string? name, string? course);
    EmailMessage ReturnedToSearching(string? name, string? course);
    EmailMessage GroupStarted(string? name, string? course, IReadOnlyList<WeeklySlot> schedule, DateTime unlockStartsAtUtc);
    EmailMessage GroupDissolved(string? name, string? course, string groupType);
    EmailMessage EnrollmentFrozen(string? name, string? course);
    EmailMessage EnrollmentUnfrozen(string? name, string? course);
    EmailMessage EnrollmentCancelled(string? name, string? course);
    EmailMessage EnrollmentCompleted(string? name, string? course);
}

public static class EmailTemplatesFactory
{
    public static IEmailTemplates Create(NotificationsOptions options) => options.Language.Trim().ToLowerInvariant() switch
    {
        "sr" => new SerbianEmailTemplates(options),
        _ => throw new InvalidOperationException($"Email language '{options.Language}' is not supported.")
    };

    public static IEmailTemplates Create(IOptions<NotificationsOptions> options) => Create(options.Value);
}

// Sastavlja mejl od pasusa i opcionog dugmeta: HTML i obicni tekst iz istog sadrzaja.
// Sve sto dolazi spolja (imena, nazivi) se HTML-enkoduje.
internal sealed class EmailBuilder
{
    private readonly string _schoolName;
    private readonly List<string> _paragraphs = new();
    private (string Text, string Url)? _button;

    public EmailBuilder(string schoolName) => _schoolName = schoolName;

    public EmailBuilder Paragraph(string text)
    {
        _paragraphs.Add(text);
        return this;
    }

    public EmailBuilder Button(string text, string url)
    {
        _button = (text, url);
        return this;
    }

    public EmailMessage Build(string subject)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><body style=\"font-family:Arial,sans-serif;font-size:15px;color:#222;line-height:1.5\">");
        foreach (var p in _paragraphs)
            html.Append("<p>").Append(WebUtility.HtmlEncode(p).Replace("\n", "<br>")).Append("</p>");
        if (_button is { } b)
            html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(b.Url))
                .Append("\" style=\"background:#c0392b;color:#fff;padding:10px 18px;border-radius:4px;text-decoration:none\">")
                .Append(WebUtility.HtmlEncode(b.Text)).Append("</a></p>");
        html.Append("<p style=\"color:#888;font-size:12px\">").Append(WebUtility.HtmlEncode(_schoolName)).Append("</p>");
        html.Append("</body></html>");

        var text = new StringBuilder();
        foreach (var p in _paragraphs)
            text.AppendLine(p).AppendLine();
        if (_button is { } bt)
            text.AppendLine($"{bt.Text}: {bt.Url}").AppendLine();
        text.AppendLine(_schoolName);

        return new EmailMessage(subject, html.ToString(), text.ToString());
    }
}
