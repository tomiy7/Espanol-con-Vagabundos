using Messaging.Contracts;
using NotificationsService.API.Data;
using NotificationsService.API.Options;
using NotificationsService.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace NotificationsService.Tests.Integration;

// Ceo put: poruka na RabbitMQ -> handler -> mejl u Mailpit-u, uz evidenciju u bazi.
[Collection(NotificationsCollection.Name)]
public class NotificationsFlowTests : IAsyncLifetime
{
    private readonly NotificationsFactory _factory;

    public NotificationsFlowTests(NotificationsFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task Publish(params Messaging.IntegrationEvent[] events)
    {
        await using var bus = await TestBus.ConnectAsync();
        foreach (var e in events) await bus.PublishAsync(e);
    }

    [IntegrationFact]
    public async Task Partner_invitation_mail_has_the_link_and_the_inviter()
    {
        var inviter = _factory.Users.Add("Marko");
        var invitee = _factory.Users.Add("Ana");
        var course = _factory.Courses.Add("Španski A1");

        await Publish(new PartnerInvited(Guid.NewGuid(), inviter.UserId, invitee.UserId, course, "tok-123",
            DateTime.UtcNow.AddDays(5)));

        var mail = Assert.Single(await _factory.Mail.WaitForAsync(invitee.Email, 1));
        Assert.Equal("Poziv za učenje u paru", mail.Subject);
        Assert.Contains("Zdravo Ana", mail.Text);
        Assert.Contains("Marko", mail.Text);
        Assert.Contains("„Španski A1“", mail.Text);
        Assert.Contains("http://front.test/invitations/tok-123", mail.Text);
        Assert.Empty(await _factory.Mail.ToAsync(inviter.Email));

        var record = Assert.Single(_factory.Sent());
        Assert.Equal(NotificationStatus.Sent, record.Status);
        Assert.Equal(invitee.Email, record.Email);
    }

    [IntegrationFact]
    public async Task The_same_event_delivered_twice_sends_one_mail()
    {
        var student = _factory.Users.Add("Ana");
        var e = new EnrollmentCompleted(Guid.NewGuid(), student.UserId, _factory.Courses.Add("A1"));

        await Publish(e, e);

        var mails = await _factory.Mail.WaitForAsync(student.Email, 1);
        await Task.Delay(1000);
        Assert.Single(await _factory.Mail.ToAsync(student.Email));
        Assert.Single(_factory.Sent());
        Assert.Single(mails);
    }

    [IntegrationFact]
    public async Task Group_start_mails_every_member_with_the_schedule()
    {
        var students = new[] { _factory.Users.Add("Ana"), _factory.Users.Add("Ivan"), _factory.Users.Add("Mila") };
        var course = _factory.Courses.Add("A2");

        await Publish(new GroupStarted(Guid.NewGuid(), course, DateTime.UtcNow, new DateTime(2026, 12, 7, 0, 0, 0, DateTimeKind.Utc))
        {
            GroupType = "Group",
            Members = students.Select(s => new GroupMemberInfo(Guid.NewGuid(), s.UserId)).ToList(),
            Slots = [new GroupSlotInfo(DayOfWeek.Monday, new TimeOnly(18, 0)), new GroupSlotInfo(DayOfWeek.Wednesday, new TimeOnly(18, 0))]
        });

        foreach (var student in students)
        {
            var mail = Assert.Single(await _factory.Mail.WaitForAsync(student.Email, 1));
            Assert.Equal("Kurs počinje", mail.Subject);
            Assert.Contains($"Zdravo {student.FullName}", mail.Text);
            Assert.Contains("ponedeljak u 18:00", mail.Text);
            Assert.Contains("sreda u 18:00", mail.Text);
        }
    }

    [IntegrationFact]
    public async Task Ready_mail_goes_only_to_real_groups_not_to_pairs_or_individuals()
    {
        var pairMember = _factory.Users.Add("Ana");
        var groupMember = _factory.Users.Add("Ivan");
        var course = _factory.Courses.Add("A1");

        await Publish(
            new GroupBecameReady(Guid.NewGuid(), course, "Pair", [])
                { Members = [new GroupMemberInfo(Guid.NewGuid(), pairMember.UserId)] },
            new GroupBecameReady(Guid.NewGuid(), course, "Group", [])
                { Members = [new GroupMemberInfo(Guid.NewGuid(), groupMember.UserId)] });

        await _factory.Mail.WaitForAsync(groupMember.Email, 1); // obradjena posle para (potrosac ide redom)
        Assert.Empty(await _factory.Mail.ToAsync(pairMember.Email));
    }

    [IntegrationFact]
    public async Task A_user_without_an_account_gets_no_mail_and_nothing_breaks()
    {
        var unknown = Guid.NewGuid();
        var known = _factory.Users.Add("Ana");
        var course = _factory.Courses.Add("A1");

        await Publish(new EnrollmentCancelled(Guid.NewGuid(), unknown, course),
            new EnrollmentCancelled(Guid.NewGuid(), known.UserId, course));

        await _factory.Mail.WaitForAsync(known.Email, 1);
        var skipped = Assert.Single(_factory.Sent(), n => n.RecipientUserId == unknown);
        Assert.Equal(NotificationStatus.Skipped, skipped.Status);
        Assert.Single(await _factory.Mail.AllAsync());
    }

    [IntegrationFact]
    public async Task When_auth_is_down_the_message_is_retried_and_dead_lettered_without_a_mail()
    {
        var student = _factory.Users.Add("Ana");
        _factory.Users.MakeUnavailable(student.UserId);
        var after = _factory.Users.Add("Ivan");

        await Publish(new EnrollmentFrozen(Guid.NewGuid(), student.UserId, Guid.NewGuid()),
            new EnrollmentFrozen(Guid.NewGuid(), after.UserId, Guid.NewGuid()));

        await _factory.Mail.WaitForAsync(after.Email, 1); // sledeca poruka i dalje prolazi
        Assert.Equal(3, _factory.Users.Calls(student.UserId)); // MaxDeliveryAttempts u testovima
        Assert.Empty(await _factory.Mail.ToAsync(student.Email));
        Assert.DoesNotContain(_factory.Sent(), n => n.RecipientUserId == student.UserId);

        await using var bus = await TestBus.ConnectAsync();
        Assert.Equal(1u, await bus.MessageCountAsync(NotificationsFactory.TestQueue + ".dead"));
        await bus.DeleteQueueAsync(NotificationsFactory.TestQueue + ".dead"); // ostali testovi broje od nule
    }

    [IntegrationFact]
    public async Task When_smtp_is_down_the_attempt_is_recorded_and_a_later_delivery_sends_the_mail_once()
    {
        const string exchange = "coursesite-tests-smtp.events";
        const string queue = "notifications-tests-smtp";
        await using (var cleanup = await TestBus.ConnectAsync(exchange))
        {
            await cleanup.DeleteQueueAsync(queue);
            await cleanup.DeleteQueueAsync(queue + ".dead");
        }

        var student = _factory.Users.Add("Ana");
        var e = new EnrollmentUnfrozen(Guid.NewGuid(), student.UserId, _factory.Courses.Add("A1"));

        // 1) SMTP nedostupan: tri pokusaja, pa dead-letter; zapis ostaje Pending sa greskom.
        await using (var smtpDown = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
                     {
                         services.Configure<SmtpOptions>(o => o.Port = 1);
                         services.Configure<Messaging.RabbitMq.RabbitMqOptions>(o => { o.Exchange = exchange; o.QueueName = queue; });
                     })))
        {
            _ = smtpDown.Server;
            await TestBus.WaitForConsumerAsync(queue, exchange);
            await using var bus = await TestBus.ConnectAsync(exchange);
            await bus.PublishAsync(e);

            var until = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < until && await bus.MessageCountAsync(queue + ".dead") == 0)
                await Task.Delay(200);
            Assert.Equal(1u, await bus.MessageCountAsync(queue + ".dead"));
        }

        var pending = Assert.Single(_factory.Sent());
        Assert.Equal(NotificationStatus.Pending, pending.Status);
        Assert.Equal(3, pending.Attempts);
        Assert.False(string.IsNullOrWhiteSpace(pending.Error));
        Assert.Empty(await _factory.Mail.ToAsync(student.Email));

        // 2) Ista poruka ponovo (npr. vracena iz dead-letter reda) kad SMTP radi: jedan mejl.
        await Publish(e);
        Assert.Single(await _factory.Mail.WaitForAsync(student.Email, 1));
        Assert.Equal(NotificationStatus.Sent, Assert.Single(_factory.Sent()).Status);
    }
}
