using Messaging;
using Messaging.Contracts;
using Microsoft.Extensions.Options;
using NotificationsService.API.Options;
using NotificationsService.API.Recipients;
using NotificationsService.API.Sending;
using NotificationsService.API.Templates;

namespace NotificationsService.API.Handlers;

// Sve sto handler-ima treba: kome i sta se salje. Jedan handler = jedan dogadjaj;
// handler samo bira primaoce i sablon, a slanje (dedup, kontakt, greske) radi Notifier.
public sealed class NotificationContext
{
    public NotificationContext(
        Notifier notifier, IEmailTemplates templates, IUserDirectory users, ICourseDirectory courses,
        IOptions<NotificationsOptions> options)
    {
        Notifier = notifier;
        Templates = templates;
        Users = users;
        Courses = courses;
        Options = options.Value;
    }

    public Notifier Notifier { get; }
    public IEmailTemplates Templates { get; }
    public IUserDirectory Users { get; }
    public ICourseDirectory Courses { get; }
    public NotificationsOptions Options { get; }

    public Task<string?> CourseAsync(Guid courseId, CancellationToken ct) => Courses.GetCourseNameAsync(courseId, ct);

    public async Task<string?> NameAsync(Guid userId, CancellationToken ct) =>
        (await Users.GetUserContactAsync(userId, ct))?.FullName;

    public Task SendAsync(IntegrationEvent e, Guid userId, string template, Func<UserContact, EmailMessage> compose,
        CancellationToken ct) => Notifier.NotifyAsync(e, userId, template, compose, ct);
}

// ---------- pozivnica za par ----------

public sealed class PartnerInvitedHandler(NotificationContext ctx) : IIntegrationEventHandler<PartnerInvited>
{
    public async Task HandleAsync(PartnerInvited e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        var inviter = await ctx.NameAsync(e.InviterUserId, ct);
        var link = $"{ctx.Options.FrontendBaseUrl.TrimEnd('/')}/invitations/{Uri.EscapeDataString(e.Token)}";
        await ctx.SendAsync(e, e.InviteeUserId, "partner-invited",
            to => ctx.Templates.PartnerInvited(to.FullName, inviter, course, link, e.ExpiresAt), ct);
    }
}

public sealed class InvitationAcceptedHandler(NotificationContext ctx) : IIntegrationEventHandler<InvitationAccepted>
{
    public async Task HandleAsync(InvitationAccepted e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        var partner = await ctx.NameAsync(e.InviteeUserId, ct);
        await ctx.SendAsync(e, e.InviterUserId, "invitation-accepted",
            to => ctx.Templates.InvitationAccepted(to.FullName, partner, course), ct);
    }
}

public sealed class InvitationDeclinedHandler(NotificationContext ctx) : IIntegrationEventHandler<InvitationDeclined>
{
    public async Task HandleAsync(InvitationDeclined e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        var partner = await ctx.NameAsync(e.InviteeUserId, ct);
        await ctx.SendAsync(e, e.InviterUserId, "invitation-declined",
            to => ctx.Templates.InvitationDeclined(to.FullName, partner, course), ct);
    }
}

// ---------- upis ----------

public sealed class EnrollmentCreatedHandler(NotificationContext ctx) : IIntegrationEventHandler<EnrollmentCreated>
{
    public async Task HandleAsync(EnrollmentCreated e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        await ctx.SendAsync(e, e.UserId, "enrollment-created",
            to => ctx.Templates.EnrollmentCreated(to.FullName, course, e.EnrollmentType, e.GroupId.HasValue), ct);
    }
}

public sealed class EnrollmentReturnedToSearchingHandler(NotificationContext ctx)
    : IIntegrationEventHandler<EnrollmentReturnedToSearching>
{
    public async Task HandleAsync(EnrollmentReturnedToSearching e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        await ctx.SendAsync(e, e.UserId, "returned-to-searching",
            to => ctx.Templates.ReturnedToSearching(to.FullName, course), ct);
    }
}

public sealed class EnrollmentFrozenHandler(NotificationContext ctx) : IIntegrationEventHandler<EnrollmentFrozen>
{
    public async Task HandleAsync(EnrollmentFrozen e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        await ctx.SendAsync(e, e.UserId, "enrollment-frozen", to => ctx.Templates.EnrollmentFrozen(to.FullName, course), ct);
    }
}

public sealed class EnrollmentUnfrozenHandler(NotificationContext ctx) : IIntegrationEventHandler<EnrollmentUnfrozen>
{
    public async Task HandleAsync(EnrollmentUnfrozen e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        await ctx.SendAsync(e, e.UserId, "enrollment-unfrozen", to => ctx.Templates.EnrollmentUnfrozen(to.FullName, course), ct);
    }
}

public sealed class EnrollmentCancelledHandler(NotificationContext ctx) : IIntegrationEventHandler<EnrollmentCancelled>
{
    public async Task HandleAsync(EnrollmentCancelled e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        await ctx.SendAsync(e, e.UserId, "enrollment-cancelled", to => ctx.Templates.EnrollmentCancelled(to.FullName, course), ct);
    }
}

public sealed class EnrollmentCompletedHandler(NotificationContext ctx) : IIntegrationEventHandler<EnrollmentCompleted>
{
    public async Task HandleAsync(EnrollmentCompleted e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        await ctx.SendAsync(e, e.UserId, "enrollment-completed", to => ctx.Templates.EnrollmentCompleted(to.FullName, course), ct);
    }
}

// ---------- grupa (mejl svakom clanu) ----------

public sealed class GroupBecameReadyHandler(NotificationContext ctx) : IIntegrationEventHandler<GroupBecameReady>
{
    public async Task HandleAsync(GroupBecameReady e, CancellationToken ct)
    {
        // Individualna grupa je "popunjena" odmah, a par cim partner prihvati - za njih
        // vec postoje mejlovi o prijavi i prihvatanju poziva.
        if (e.GroupType != "Group")
            return;

        var course = await ctx.CourseAsync(e.CourseId, ct);
        foreach (var member in e.Members)
            await ctx.SendAsync(e, member.UserId, "group-ready", to => ctx.Templates.GroupReady(to.FullName, course), ct);
    }
}

public sealed class GroupStartedHandler(NotificationContext ctx) : IIntegrationEventHandler<GroupStarted>
{
    public async Task HandleAsync(GroupStarted e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        var schedule = e.Slots.Select(s => new WeeklySlot(s.DayOfWeek, s.Time)).ToList();
        foreach (var member in e.Members)
            await ctx.SendAsync(e, member.UserId, "group-started",
                to => ctx.Templates.GroupStarted(to.FullName, course, schedule, e.UnlockStartsAt), ct);
    }
}

// Pomeren datum prvog casa: mejl svim clanovima grupe.
public sealed class FirstLessonRescheduledHandler(NotificationContext ctx) : IIntegrationEventHandler<FirstLessonRescheduled>
{
    public async Task HandleAsync(FirstLessonRescheduled e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        foreach (var member in e.Members)
            await ctx.SendAsync(e, member.UserId, "first-lesson-rescheduled",
                to => ctx.Templates.FirstLessonRescheduled(to.FullName, course, e.OldFirstLessonAt, e.NewFirstLessonAt), ct);
    }
}

public sealed class GroupDissolvedHandler(NotificationContext ctx) : IIntegrationEventHandler<GroupDissolved>
{
    public async Task HandleAsync(GroupDissolved e, CancellationToken ct)
    {
        var course = await ctx.CourseAsync(e.CourseId, ct);
        foreach (var member in e.Members)
            await ctx.SendAsync(e, member.UserId, "group-dissolved",
                to => ctx.Templates.GroupDissolved(to.FullName, course, e.GroupType), ct);
    }
}
