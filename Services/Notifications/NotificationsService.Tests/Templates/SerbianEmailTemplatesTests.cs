using NotificationsService.API.Options;
using NotificationsService.API.Templates;

namespace NotificationsService.Tests.Templates;

public class SerbianEmailTemplatesTests
{
    private static readonly IEmailTemplates Templates =
        EmailTemplatesFactory.Create(new NotificationsOptions { Language = "sr", TimeZone = "Europe/Belgrade", SchoolName = "Škola" });

    // Svaki sablon, sa podacima i bez njih (Auth/Courses nisu odgovorili).
    public static TheoryData<string, EmailMessage> AllMessages()
    {
        var data = new TheoryData<string, EmailMessage>();
        var slots = new[] { new WeeklySlot(DayOfWeek.Monday, new TimeOnly(18, 0)) };
        var at = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);
        foreach (var (name, partner, course) in new[] { ("Ana", "Marko", "Španski A1"), ((string?)null, (string?)null, (string?)null) })
        {
            var suffix = name is null ? " (no data)" : "";
            data.Add("invited" + suffix, Templates.PartnerInvited(name, partner, course, "http://front/invitations/abc", at));
            data.Add("accepted" + suffix, Templates.InvitationAccepted(name, partner, course));
            data.Add("declined" + suffix, Templates.InvitationDeclined(name, partner, course));
            foreach (var type in new[] { "Individual", "Group", "Pair" })
                data.Add($"created {type}" + suffix, Templates.EnrollmentCreated(name, course, type, alreadyInGroup: false));
            data.Add("created Pair in group" + suffix, Templates.EnrollmentCreated(name, course, "Pair", alreadyInGroup: true));
            data.Add("ready" + suffix, Templates.GroupReady(name, course));
            data.Add("searching" + suffix, Templates.ReturnedToSearching(name, course));
            data.Add("started" + suffix, Templates.GroupStarted(name, course, slots, at));
            data.Add("dissolved Pair" + suffix, Templates.GroupDissolved(name, course, "Pair"));
            data.Add("dissolved Group" + suffix, Templates.GroupDissolved(name, course, "Group"));
            data.Add("rescheduled" + suffix, Templates.FirstLessonRescheduled(name, course, at, at.AddDays(7)));
            data.Add("frozen" + suffix, Templates.EnrollmentFrozen(name, course));
            data.Add("unfrozen" + suffix, Templates.EnrollmentUnfrozen(name, course));
            data.Add("cancelled" + suffix, Templates.EnrollmentCancelled(name, course));
            data.Add("completed" + suffix, Templates.EnrollmentCompleted(name, course));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllMessages))]
    public void Every_message_is_complete(string _, EmailMessage message)
    {
        Assert.False(string.IsNullOrWhiteSpace(message.Subject));
        Assert.StartsWith("<!DOCTYPE html>", message.Html);
        Assert.Contains("Zdravo", message.Text);
        Assert.Contains("Škola", message.Text);
        foreach (var leftover in new[] { "{", "}", "  ,", "„“", "null" })
        {
            Assert.DoesNotContain(leftover, message.Text);
        }
    }

    [Fact]
    public void Invitation_contains_the_link_the_inviter_and_the_local_expiry_time()
    {
        var message = Templates.PartnerInvited("Ana", "Marko", "Španski A1", "http://front/invitations/abc",
            new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc));

        Assert.Contains("Marko", message.Text);
        Assert.Contains("„Španski A1“", message.Text);
        Assert.Contains("http://front/invitations/abc", message.Text);
        Assert.Contains("href=\"http://front/invitations/abc\"", message.Html);
        Assert.Contains("1. 12. 2026. u 11:00", message.Text); // UTC 10:00 = 11:00 u Beogradu (zimsko vreme)
    }

    [Fact]
    public void Start_lists_the_schedule_in_serbian()
    {
        var message = Templates.GroupStarted("Ana", "Španski A1",
            [new WeeklySlot(DayOfWeek.Monday, new TimeOnly(18, 0)), new WeeklySlot(DayOfWeek.Thursday, new TimeOnly(9, 30))],
            new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Contains("ponedeljak u 18:00", message.Text);
        Assert.Contains("četvrtak u 09:30", message.Text);
        Assert.Contains("1. 12. 2026.", message.Text);
    }

    [Fact]
    public void Rescheduled_first_lesson_shows_new_and_old_local_time()
    {
        var message = Templates.FirstLessonRescheduled("Ana", "Španski A1",
            new DateTime(2026, 12, 1, 17, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 8, 17, 0, 0, DateTimeKind.Utc));

        Assert.Equal("Prvi čas je pomeren", message.Subject);
        Assert.Contains("„Španski A1“", message.Text);
        Assert.Contains("Novi termin: 8. 12. 2026. u 18:00 (umesto 1. 12. 2026. u 18:00)", message.Text);
    }

    [Fact]
    public void Values_from_users_are_html_encoded()
    {
        var message = Templates.InvitationAccepted("<script>alert(1)</script>", "Marko & Co", "A1");

        Assert.DoesNotContain("<script>", message.Html);
        Assert.Contains("&lt;script&gt;", message.Html);
        Assert.Contains("Marko &amp; Co", message.Html);
    }

    [Fact]
    public void An_unsupported_language_fails_at_startup()
    {
        Assert.Throws<InvalidOperationException>(() => EmailTemplatesFactory.Create(new NotificationsOptions { Language = "de" }));
    }
}
