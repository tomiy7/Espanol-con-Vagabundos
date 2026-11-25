using System.Net;
using System.Text.Json;
using GroupsService.Integration.Tests.Infrastructure;

namespace GroupsService.Integration.Tests.Messaging;

// Dogadjaji koje koristi Notifications (i kasnije Payments) moraju da nose KOME se nesto
// desilo: korisnike, kurs, raspored, link pozivnice. Proverava se sadrzaj u outbox-u.
public class NotificationEventsTests : ApiTestBase
{
    public NotificationEventsTests(ApiFactory factory) : base(factory) { }

    private List<(string Type, JsonElement Data)> Events() =>
        Factory.WithDb(db => db.OutboxMessages.OrderBy(m => m.OccurredAt).ToList())
            .Select(m => (m.Type, JsonDocument.Parse(m.Payload).RootElement.Clone()))
            .ToList();

    private JsonElement Single(string type) => Assert.Single(Events(), e => e.Type == type).Data;

    private static List<(Guid EnrollmentId, Guid UserId)> Members(JsonElement data) =>
        data.GetProperty("members").EnumerateArray()
            .Select(m => (m.GetProperty("enrollmentId").GetGuid(), m.GetProperty("userId").GetGuid()))
            .OrderBy(m => m.Item1).ToList();

    private async Task<(TestUser Inviter, TestUser Invitee)> Invite()
    {
        var inviter = TestUser.Student();
        var invitee = TestUser.Student();
        await AssertStatus(HttpStatusCode.Created,
            await Post(inviter, "/enrollments/invite-partner", new { courseId = CourseId, inviteeUserId = invitee.Id }));
        return (inviter, invitee);
    }

    private string InvitationToken() => Factory.WithDb(db => db.PartnerInvitations.Single().Token);

    [IntegrationFact]
    public async Task Partner_invitation_carries_who_invites_whom_and_the_link_token()
    {
        var (inviter, invitee) = await Invite();
        var invitation = Factory.WithDb(db => db.PartnerInvitations.Single());

        var data = Single("PartnerInvited");
        Assert.Equal(invitation.Id, data.GetProperty("invitationId").GetGuid());
        Assert.Equal(inviter.Id, data.GetProperty("inviterUserId").GetGuid());
        Assert.Equal(invitee.Id, data.GetProperty("inviteeUserId").GetGuid());
        Assert.Equal(CourseId, data.GetProperty("courseId").GetGuid());
        Assert.Equal(invitation.Token, data.GetProperty("token").GetString());
        Assert.Equal(invitation.ExpiresAt, data.GetProperty("expiresAt").GetDateTime(), TimeSpan.FromMilliseconds(1));

        var created = Single("EnrollmentCreated");
        Assert.Equal(inviter.Id, created.GetProperty("userId").GetGuid());
        Assert.Equal("Pair", created.GetProperty("enrollmentType").GetString());
    }

    [IntegrationFact]
    public async Task Accepting_tells_the_inviter_and_lists_both_partners_in_the_ready_pair()
    {
        var (inviter, invitee) = await Invite();
        var accepted = await Post(null, $"/invitations/{InvitationToken()}/accept");
        await AssertStatus(HttpStatusCode.OK, accepted);
        var groupId = (await Json(accepted)).GetProperty("groupId").GetGuid();

        var data = Single("InvitationAccepted");
        Assert.Equal(groupId, data.GetProperty("groupId").GetGuid());
        Assert.Equal(inviter.Id, data.GetProperty("inviterUserId").GetGuid());
        Assert.Equal(invitee.Id, data.GetProperty("inviteeUserId").GetGuid());

        var ready = Single("GroupBecameReady");
        Assert.Equal("Pair", ready.GetProperty("groupType").GetString());
        Assert.Equal(new[] { inviter.Id, invitee.Id }.OrderBy(x => x), Members(ready).Select(m => m.UserId).OrderBy(x => x));
    }

    [IntegrationFact]
    public async Task Declining_tells_the_inviter()
    {
        var (inviter, invitee) = await Invite();
        await AssertStatus(HttpStatusCode.NoContent, await Post(null, $"/invitations/{InvitationToken()}/decline"));

        var data = Single("InvitationDeclined");
        Assert.Equal(inviter.Id, data.GetProperty("inviterUserId").GetGuid());
        Assert.Equal(invitee.Id, data.GetProperty("inviteeUserId").GetGuid());
        Assert.Equal(CourseId, data.GetProperty("courseId").GetGuid());
    }

    [IntegrationFact]
    public async Task Group_start_lists_every_member_and_the_weekly_schedule()
    {
        var (groupId, professor, students, ids) = await ReadyGroup(hour: 17);
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));

        var data = Single("GroupStarted");
        Assert.Equal("Group", data.GetProperty("groupType").GetString());
        Assert.Equal(ids.Zip(students, (id, s) => (id, s.Id)).OrderBy(m => m.id).ToList(), Members(data));

        var slots = data.GetProperty("slots").EnumerateArray()
            .Select(s => $"{s.GetProperty("dayOfWeek").GetString()} {s.GetProperty("time").GetString()}").ToList();
        Assert.Equal(new[] { "Monday 17:00:00", "Wednesday 17:00:00", "Friday 17:00:00" }, slots);
    }

    [IntegrationFact]
    public async Task Freeze_and_unfreeze_name_the_student()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/enrollments/{ids[0]}/freeze"));
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/enrollments/{ids[0]}/unfreeze"));

        foreach (var type in new[] { "EnrollmentFrozen", "EnrollmentUnfrozen" })
        {
            var data = Single(type);
            Assert.Equal(ids[0], data.GetProperty("enrollmentId").GetGuid());
            Assert.Equal(students[0].Id, data.GetProperty("userId").GetGuid());
            Assert.Equal(CourseId, data.GetProperty("courseId").GetGuid());
        }
    }

    [IntegrationFact]
    public async Task When_a_ready_group_loses_a_member_the_others_are_told_it_is_waiting_again()
    {
        var (_, _, students, ids) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(students[0], $"/enrollments/{ids[0]}/cancel"));

        var returned = Events().Where(e => e.Type == "EnrollmentReturnedToSearching")
            .Select(e => e.Data.GetProperty("userId").GetGuid()).OrderBy(x => x);
        Assert.Equal(new[] { students[1].Id, students[2].Id }.OrderBy(x => x), returned);
        Assert.Equal(students[0].Id, Single("EnrollmentCancelled").GetProperty("userId").GetGuid());
    }

    [IntegrationFact]
    public async Task When_a_partner_cancels_the_dissolved_pair_names_the_one_who_stays()
    {
        var (inviter, invitee) = await Invite();
        await AssertStatus(HttpStatusCode.OK, await Post(null, $"/invitations/{InvitationToken()}/accept"));
        var inviteeEnrollment = Factory.WithDb(db => db.Enrollments.Single(e => e.UserId == invitee.Id).Id);

        await AssertStatus(HttpStatusCode.NoContent, await Post(invitee, $"/enrollments/{inviteeEnrollment}/cancel"));

        var dissolved = Single("GroupDissolved");
        Assert.Equal("Pair", dissolved.GetProperty("groupType").GetString());
        Assert.Equal(new[] { inviter.Id }, Members(dissolved).Select(m => m.UserId));
    }

    [IntegrationFact]
    public async Task Group_members_store_the_user_of_each_enrollment()
    {
        var (groupId, _, students, ids) = await ReadyGroup();

        var stored = Factory.WithDb(db => db.Groups.Where(g => g.Id == groupId)
            .SelectMany(g => g.Members).Select(m => new { m.EnrollmentId, m.UserId }).ToList());

        Assert.Equal(ids.Zip(students, (id, s) => (id, s.Id)).OrderBy(m => m.id),
            stored.Select(m => (m.EnrollmentId, m.UserId)).OrderBy(m => m.EnrollmentId));
    }
}
