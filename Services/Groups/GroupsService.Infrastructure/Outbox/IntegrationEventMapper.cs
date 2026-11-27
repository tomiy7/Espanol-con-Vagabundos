using GroupsService.Domain;
using GroupsService.Domain.Entities;
using Messaging;
using Contracts = Messaging.Contracts;
using DomainEvents = GroupsService.Domain.Events;

namespace GroupsService.Infrastructure.Outbox;

// Koji domenski dogadjaji postaju javni (integracioni) i sa kojim podacima. Podaci se
// uzimaju iz agregata u trenutku snimanja, pa poruka opisuje stanje koje je upisano.
// Ostali domenski dogadjaji (clan usao, termin dodeljen...) su interni i ne izlaze.
public static class IntegrationEventMapper
{
    public static IntegrationEvent? Map(AggregateRoot aggregate, object domainEvent) => (aggregate, domainEvent) switch
    {
        // ---------- grupa ----------
        (Group g, DomainEvents.GroupBecameReady) => new Contracts.GroupBecameReady(
            g.Id, g.CourseId, g.Type.ToString(), g.Members.Select(m => m.EnrollmentId).ToList())
        {
            Members = Members(g)
        },

        (Group g, DomainEvents.GroupStarted) => new Contracts.GroupStarted(
            g.Id, g.CourseId, g.StartedAt!.Value, g.UnlockStartsAt!.Value)
        {
            GroupType = g.Type.ToString(),
            Members = Members(g),
            Slots = g.Slots
                .Where(s => s.IsAssigned)
                .OrderBy(s => s.SlotIndex)
                .Select(s => new Contracts.GroupSlotInfo(s.DayOfWeek!.Value, s.Time!.Value))
                .ToList()
        },

        // Posle rasformiranja grupa nema clanova - spisak nosi sam domenski dogadjaj.
        (Group g, DomainEvents.GroupDissolved e) => new Contracts.GroupDissolved(g.Id, g.CourseId)
        {
            GroupType = g.Type.ToString(),
            Members = e.FormerMembers.Select(m => new Contracts.GroupMemberInfo(m.EnrollmentId, m.UserId)).ToList()
        },

        (Group g, DomainEvents.GroupCompleted) => new Contracts.GroupCompleted(g.Id, g.CourseId)
        {
            Members = Members(g)
        },

        (Group g, DomainEvents.GroupConfirmed) => new Contracts.GroupConfirmed(
            g.Id, g.CourseId, g.Type.ToString(), g.PlannedStartAt!.Value, Members(g)),

        (Group g, DomainEvents.MemberJoinedConfirmedGroup e) => new Contracts.MemberJoinedConfirmedGroup(
            g.Id, g.CourseId, g.Type.ToString(), g.PlannedStartAt!.Value,
            new Contracts.GroupMemberInfo(e.EnrollmentId, e.UserId)),

        (Group g, DomainEvents.FirstLessonRescheduled e) => new Contracts.FirstLessonRescheduled(
            g.Id, g.CourseId, e.OldAt, e.NewAt, Members(g)),

        // ---------- upis ----------
        (Enrollment e, DomainEvents.EnrollmentCreated) => new Contracts.EnrollmentCreated(
            e.Id, e.UserId, e.CourseId, e.Type.ToString(), e.GroupId),

        (Enrollment e, DomainEvents.EnrollmentCancelled) => new Contracts.EnrollmentCancelled(e.Id, e.UserId, e.CourseId),

        (Enrollment e, DomainEvents.EnrollmentCompleted) => new Contracts.EnrollmentCompleted(e.Id, e.UserId, e.CourseId),

        (Enrollment e, DomainEvents.EnrollmentFrozen) => new Contracts.EnrollmentFrozen(e.Id, e.UserId, e.CourseId),

        (Enrollment e, DomainEvents.EnrollmentUnfrozen) => new Contracts.EnrollmentUnfrozen(e.Id, e.UserId, e.CourseId),

        (Enrollment e, DomainEvents.EnrollmentReturnedToSearching) =>
            new Contracts.EnrollmentReturnedToSearching(e.Id, e.UserId, e.CourseId),

        // ---------- pozivnica za par ----------
        (PartnerInvitation i, DomainEvents.PartnerInvited) => new Contracts.PartnerInvited(
            i.Id, i.InviterUserId, i.InviteeUserId, i.CourseId, i.Token, i.ExpiresAt),

        (PartnerInvitation i, DomainEvents.InvitationAccepted a) => new Contracts.InvitationAccepted(
            i.Id, a.GroupId, i.InviterUserId, i.InviteeUserId, i.CourseId),

        (PartnerInvitation i, DomainEvents.InvitationDeclined) => new Contracts.InvitationDeclined(
            i.Id, i.InviterUserId, i.InviteeUserId, i.CourseId),

        _ => null
    };

    private static List<Contracts.GroupMemberInfo> Members(Group g) =>
        g.Members.Select(m => new Contracts.GroupMemberInfo(m.EnrollmentId, m.UserId)).ToList();
}
