using System.Net;
using System.Text;
using System.Text.Json;
using GroupsService.Integration.Tests.Infrastructure;
using Messaging.Contracts;

namespace GroupsService.Integration.Tests.Messaging;

// Outbox (dogadjaj se upisuje u istoj transakciji kao promena) i put kroz pravi RabbitMQ:
// Groups objavljuje svoje dogadjaje i obradjuje CourseCompleted iz Courses.
public class MessagingTests : ApiTestBase
{
    public MessagingTests(ApiFactory factory) : base(factory) { }

    private List<(Guid Id, string Type, JsonElement Payload, bool Processed)> Outbox() =>
        Factory.WithDb(db => db.OutboxMessages.OrderBy(m => m.OccurredAt).ToList())
            .Select(m => (m.Id, m.Type, JsonDocument.Parse(m.Payload).RootElement.Clone(), m.ProcessedAt != null))
            .ToList();

    // ---------- outbox (bez RabbitMQ-a) ----------

    [IntegrationFact]
    public async Task Business_changes_write_their_events_to_the_outbox()
    {
        var (groupId, professor, _, ids) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/enrollments/{ids[0]}/cancel"));

        var outbox = Outbox();
        Assert.Equal(new[] { "GroupBecameReady", "GroupStarted", "EnrollmentCancelled" }, outbox.Select(m => m.Type));

        var ready = outbox[0].Payload;
        Assert.Equal(groupId, ready.GetProperty("groupId").GetGuid());
        Assert.Equal(CourseId, ready.GetProperty("courseId").GetGuid());
        Assert.Equal("Group", ready.GetProperty("groupType").GetString());
        Assert.Equal(ids.OrderBy(x => x), ready.GetProperty("memberEnrollmentIds").EnumerateArray().Select(x => x.GetGuid()).OrderBy(x => x));

        var cancelled = outbox[2].Payload;
        Assert.Equal(ids[0], cancelled.GetProperty("enrollmentId").GetGuid());
        Assert.Equal(outbox[2].Id, cancelled.GetProperty("eventId").GetGuid());
    }

    [IntegrationFact]
    public async Task Rejected_operations_write_no_events()
    {
        var professor = TestUser.Professor();
        var groupId = await CreateGroup(professor);
        await AddMember(professor, groupId, TestUser.Student());

        await AssertStatus(HttpStatusCode.BadRequest, await Post(professor, $"/groups/{groupId}/start"));

        Assert.Empty(Outbox());
    }

    // ---------- pravi RabbitMQ ----------

    [MessagingFact]
    public async Task Outbox_events_are_published_to_rabbitmq_and_marked_as_sent()
    {
        await using var bus = await TestBus.ConnectAsync();
        var queue = await bus.ListenAsync("GroupStarted");
        var (groupId, professor, _, _) = await ReadyGroup();

        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));

        var message = await bus.ReceiveAsync(queue, TimeSpan.FromSeconds(15));
        Assert.NotNull(message);
        using var envelope = JsonDocument.Parse(message);
        Assert.Equal("GroupStarted", envelope.RootElement.GetProperty("eventType").GetString());
        Assert.Equal(groupId, envelope.RootElement.GetProperty("data").GetProperty("groupId").GetGuid());

        var eventId = envelope.RootElement.GetProperty("eventId").GetGuid();
        await Eventually(() => Outbox().Single(m => m.Id == eventId).Processed, "the outbox message is marked as sent");
    }

    [MessagingFact]
    public async Task CourseCompleted_from_courses_completes_the_enrollment()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));
        await using var bus = await TestBus.ConnectAsync();

        await bus.PublishAsync(new CourseCompleted(ids[0], students[0].Id, CourseId));

        await Eventually(() => Factory.WithDb(db => db.Enrollments.Single(e => e.Id == ids[0]).Status.ToString()) == "Completed",
            "the enrollment is completed");
    }

    [MessagingFact]
    public async Task Duplicate_CourseCompleted_messages_are_harmless()
    {
        var (groupId, professor, students, ids) = await ReadyGroup();
        await AssertStatus(HttpStatusCode.NoContent, await Post(professor, $"/groups/{groupId}/start"));
        await using var bus = await TestBus.ConnectAsync();
        var message = new CourseCompleted(ids[0], students[0].Id, CourseId);

        await bus.PublishAsync(message);
        await bus.PublishAsync(message);

        await Eventually(() => Factory.WithDb(db => db.Enrollments.Single(e => e.Id == ids[0]).Status.ToString()) == "Completed",
            "the enrollment is completed");
        await Task.Delay(500);
        Assert.Single(Outbox(), m => m.Type == "EnrollmentCompleted");
    }

    [MessagingFact]
    public async Task Unreadable_message_goes_to_the_dead_letter_queue()
    {
        await using var bus = await TestBus.ConnectAsync();

        await bus.PublishRawAsync("CourseCompleted", Encoding.UTF8.GetBytes("this is not json"));

        var dead = await bus.ReceiveAsync(ApiFactory.TestQueue + ".dead", TimeSpan.FromSeconds(15));
        Assert.Equal("this is not json", dead);
    }
}
