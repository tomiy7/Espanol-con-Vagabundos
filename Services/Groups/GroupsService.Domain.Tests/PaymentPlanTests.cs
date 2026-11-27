using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Events;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Domain.Tests;

// Izbor placanja (odjednom / rate) i potvrda grupe pri startu (da svi dobiju uplatnice).
public class PaymentPlanTests
{
    private static Group StartableGroup(int members)
    {
        var g = Group.CreateGroup(Guid.NewGuid(), 7);
        for (var i = 0; i < members; i++)
            g.AddMember(Guid.NewGuid(), Guid.NewGuid());
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        for (var i = 0; i < Group.WeeklySlotCount; i++)
            g.AssignProfessorToSlot(i, days[i], new TimeOnly(18, 0), Guid.NewGuid());
        g.ClearDomainEvents();
        return g;
    }

    [Fact]
    public void Enrollment_defaults_to_full_payment()
    {
        var e = Enrollment.CreateSearching(Guid.NewGuid(), Guid.NewGuid(), EnrollmentType.Group);

        Assert.Equal(PaymentPlan.Full, e.PaymentPlan);
    }

    [Fact]
    public void Chosen_plan_is_kept_on_enrollment_and_group_member()
    {
        var e = Enrollment.CreateIndividual(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PaymentPlan.Installments);
        var g = Group.CreateIndividual(e.CourseId);

        g.AddMember(e.Id, e.UserId, e.PaymentPlan);

        Assert.Equal(PaymentPlan.Installments, e.PaymentPlan);
        Assert.Equal(PaymentPlan.Installments, g.Members.Single().PaymentPlan);
    }

    [Fact]
    public void Undefined_plan_value_is_rejected()
    {
        Assert.Throws<GroupsDomainException>(() =>
            Enrollment.CreateSearching(Guid.NewGuid(), Guid.NewGuid(), EnrollmentType.Group, (PaymentPlan)7));
    }

    [Fact]
    public void Member_plan_can_change_before_confirmation_but_not_after()
    {
        var g = StartableGroup(2);
        var member = g.Members.First().EnrollmentId;

        g.ChangeMemberPaymentPlan(member, PaymentPlan.Installments);
        Assert.Equal(PaymentPlan.Installments, g.Members.First().PaymentPlan);

        g.AddMember(Guid.NewGuid(), Guid.NewGuid());
        g.SchedulePlannedStart(DateTime.UtcNow.AddDays(5), DateTime.UtcNow);

        Assert.Throws<GroupsDomainException>(() => g.ChangeMemberPaymentPlan(member, PaymentPlan.Full));
    }

    [Fact]
    public void Plan_of_cancelled_enrollment_cannot_change()
    {
        var e = Enrollment.CreateSearching(Guid.NewGuid(), Guid.NewGuid(), EnrollmentType.Group);
        e.Cancel();

        Assert.Throws<GroupsDomainException>(() => e.ChangePaymentPlan(PaymentPlan.Installments));
    }

    [Fact]
    public void Start_confirms_a_ready_group_without_planned_date_using_the_start_time()
    {
        var g = StartableGroup(3);
        var now = new DateTime(2026, 12, 1, 17, 0, 0, DateTimeKind.Utc);

        g.Start(now: now);

        Assert.Equal(now, g.ConfirmedAt);
        Assert.Equal(now, g.PlannedStartAt);
        Assert.Single(g.DomainEvents.OfType<GroupConfirmed>());
    }

    [Fact]
    public void Forced_start_below_minimum_also_confirms()
    {
        var g = StartableGroup(1);

        g.Start(force: true);

        Assert.NotNull(g.ConfirmedAt);
        Assert.Single(g.DomainEvents.OfType<GroupConfirmed>());
    }

    [Fact]
    public void Start_does_not_confirm_an_already_confirmed_group_again()
    {
        var g = StartableGroup(3);
        var planned = DateTime.UtcNow.AddDays(3);
        g.SchedulePlannedStart(planned, DateTime.UtcNow);
        var confirmedAt = g.ConfirmedAt;
        g.ClearDomainEvents();

        g.Start();

        Assert.Equal(confirmedAt, g.ConfirmedAt);
        Assert.Equal(planned, g.PlannedStartAt);
        Assert.Empty(g.DomainEvents.OfType<GroupConfirmed>());
    }
}
