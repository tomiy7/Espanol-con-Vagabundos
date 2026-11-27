using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroupsService.Integration.Tests.Infrastructure;

namespace GroupsService.Integration.Tests.Flows;

// Izbor placanja pri upisu (odjednom / rate), promena pre potvrde grupe i prenos u dogadjaje za Payments.
public class PaymentPlanTests : ApiTestBase
{
    public PaymentPlanTests(ApiFactory factory) : base(factory) { }

    private static readonly DateTime FirstLesson = DateTime.UtcNow.Date.AddDays(14).AddHours(17);

    private List<(string Type, JsonElement Data)> Events() =>
        Factory.WithDb(db => db.OutboxMessages.OrderBy(m => m.OccurredAt).ToList())
            .Select(m => (m.Type, JsonDocument.Parse(m.Payload).RootElement.Clone()))
            .ToList();

    private Task<HttpResponseMessage> ChangePlan(TestUser user, Guid enrollmentId, object plan) =>
        ClientFor(user).PutAsJsonAsync($"/enrollments/{enrollmentId}/payment-plan", new { paymentPlan = plan });

    private async Task<string> PlanOf(TestUser student) =>
        (await MyEnrollments(student))[0].GetProperty("paymentPlan").GetString()!;

    [IntegrationFact]
    public async Task Default_plan_is_full_payment()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        var student = TestUser.Student();

        await AddMember(professor, groupId, student);

        Assert.Equal("Full", await PlanOf(student));
    }

    [IntegrationFact]
    public async Task Every_enrollment_path_keeps_the_chosen_plan()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);

        var byStaff = TestUser.Student();
        await AssertStatus(HttpStatusCode.Created, await Post(professor, $"/groups/{groupId}/members",
            new { userId = byStaff.Id, paymentPlan = "Installments" }));

        var self = TestUser.Student();
        await AssertStatus(HttpStatusCode.Created, await Post(self, "/enrollments/group",
            new { groupId, paymentPlan = "Installments" }));

        var individual = TestUser.Student();
        await AssertStatus(HttpStatusCode.Created, await Post(individual, "/enrollments/individual",
            new { courseId = CourseId, professorId = professor.Id, paymentPlan = "Installments" }));

        var inviter = TestUser.Student();
        var invitee = TestUser.Student();
        await AssertStatus(HttpStatusCode.Created, await Post(inviter, "/enrollments/invite-partner",
            new { courseId = CourseId, inviteeUserId = invitee.Id, paymentPlan = "Installments" }));
        var token = Factory.WithDb(db => db.PartnerInvitations.Single().Token);
        await AssertStatus(HttpStatusCode.OK, await Post(null, $"/invitations/{token}/accept",
            new { paymentPlan = "Installments" }));

        foreach (var student in new[] { byStaff, self, individual, inviter, invitee })
            Assert.Equal("Installments", await PlanOf(student));
    }

    [IntegrationFact]
    public async Task Accepting_without_a_body_still_works_and_means_full_payment()
    {
        var inviter = TestUser.Student();
        var invitee = TestUser.Student();
        await Post(inviter, "/enrollments/invite-partner", new { courseId = CourseId, inviteeUserId = invitee.Id });
        var token = Factory.WithDb(db => db.PartnerInvitations.Single().Token);

        var response = await ClientFor(null).PostAsync($"/invitations/{token}/accept", null);

        await AssertStatus(HttpStatusCode.OK, response);
        Assert.Equal("Full", await PlanOf(invitee));
    }

    [IntegrationFact]
    public async Task Unknown_plan_values_are_rejected()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);

        var text = await Post(professor, $"/groups/{groupId}/members", new { userId = Guid.NewGuid(), paymentPlan = "Monthly" });
        var number = await Post(professor, $"/groups/{groupId}/members", new { userId = Guid.NewGuid(), paymentPlan = 7 });

        Assert.Equal(HttpStatusCode.BadRequest, text.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, number.StatusCode);
        Assert.Empty(Factory.WithDb(db => db.Enrollments.ToList()));
    }

    [IntegrationFact]
    public async Task Student_changes_own_plan_before_confirmation_and_it_reaches_the_event()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();

        await AssertStatus(HttpStatusCode.NoContent, await ChangePlan(students[0], ids[0], "Installments"));
        await AssertStatus(HttpStatusCode.NoContent,
            await ClientFor(professor).PutAsJsonAsync($"/groups/{groupId}/planned-start", new { plannedStartAt = FirstLesson }));

        Assert.Equal("Installments", await PlanOf(students[0]));
        var confirmed = Assert.Single(Events(), e => e.Type == "GroupConfirmed").Data;
        var plans = confirmed.GetProperty("members").EnumerateArray()
            .ToDictionary(m => m.GetProperty("enrollmentId").GetGuid(), m => m.GetProperty("paymentPlan").GetString());
        Assert.Equal("Installments", plans[ids[0]]);
        Assert.Equal("Full", plans[ids[1]]);
    }

    [IntegrationFact]
    public async Task Plan_cannot_change_in_groups_after_confirmation()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await ClientFor(professor).PutAsJsonAsync($"/groups/{groupId}/planned-start", new { plannedStartAt = FirstLesson });

        await AssertProblem(HttpStatusCode.BadRequest, "CANNOT_CHANGE_PAYMENT_PLAN",
            await ChangePlan(students[0], ids[0], "Installments"));
        Assert.Equal("Full", await PlanOf(students[0]));
    }

    [IntegrationFact]
    public async Task Other_students_cannot_change_my_plan_but_staff_can_and_it_is_audited()
    {
        var (_, professor, students, ids) = await ReadyGroup();

        await AssertStatus(HttpStatusCode.NotFound, await ChangePlan(students[1], ids[0], "Installments"));
        Assert.Equal("Full", await PlanOf(students[0]));

        await AssertStatus(HttpStatusCode.NoContent, await ChangePlan(professor, ids[0], "Installments"));
        Assert.Equal("Installments", await PlanOf(students[0]));

        var audit = await Json(await Get(TestUser.Admin(), $"/audit?entityId={ids[0]}"));
        Assert.Contains(audit.EnumerateArray(), a => a.GetProperty("action").GetString() == "ChangePaymentPlan");
    }

    [IntegrationFact]
    public async Task Late_member_event_carries_the_members_plan()
    {
        var (groupId, professor, _, _) = await ReadyGroup();
        await ClientFor(professor).PutAsJsonAsync($"/groups/{groupId}/planned-start", new { plannedStartAt = FirstLesson });
        var late = TestUser.Student();

        await Post(professor, $"/groups/{groupId}/members", new { userId = late.Id, paymentPlan = "Installments" });

        var joined = Assert.Single(Events(), e => e.Type == "MemberJoinedConfirmedGroup").Data;
        Assert.Equal("Installments", joined.GetProperty("member").GetProperty("paymentPlan").GetString());
    }

    [IntegrationFact]
    public async Task Starting_without_a_planned_date_confirms_the_group_with_the_start_as_first_lesson()
    {
        var (groupId, professor, students, _) = await ReadyGroup();

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));

        var group = await GetGroup(professor, groupId);
        var startedAt = group.GetProperty("startedAt").GetDateTime();
        var confirmed = Assert.Single(Events(), e => e.Type == "GroupConfirmed").Data;
        Assert.Equal(startedAt, confirmed.GetProperty("firstLessonAt").GetDateTime(), TimeSpan.FromMilliseconds(1));
        Assert.Equal(students.Count, confirmed.GetProperty("members").GetArrayLength());
    }
}
