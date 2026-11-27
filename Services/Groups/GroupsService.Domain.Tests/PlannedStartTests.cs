using GroupsService.Domain.Entities;
using GroupsService.Domain.Events;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Domain.Tests;

// Planirani datum prvog casa i "potvrdjena grupa" (dovoljno clanova + datum).
public class PlannedStartTests
{
    private static DateTime Now => DateTime.UtcNow;

    private static Group GroupWith(int members)
    {
        var g = Group.CreateGroup(Guid.NewGuid(), 7);
        for (var i = 0; i < members; i++)
            g.AddMember(Guid.NewGuid(), Guid.NewGuid());
        g.ClearDomainEvents();
        return g;
    }

    private static int Count<T>(Group g) => g.DomainEvents.OfType<T>().Count();

    [Fact]
    public void Date_on_forming_group_is_saved_without_confirming()
    {
        var g = GroupWith(2);
        var at = Now.AddDays(10);

        g.SchedulePlannedStart(at, Now);

        Assert.Equal(at, g.PlannedStartAt);
        Assert.Null(g.ConfirmedAt);
        Assert.Equal(0, Count<GroupConfirmed>(g));
        Assert.Equal(1, Count<FirstLessonScheduled>(g));
    }

    [Fact]
    public void Ready_group_with_date_is_confirmed()
    {
        var g = GroupWith(3);

        g.SchedulePlannedStart(Now.AddDays(10), Now);

        Assert.NotNull(g.ConfirmedAt);
        Assert.Equal(1, Count<GroupConfirmed>(g));
    }

    [Fact]
    public void Date_first_then_members_confirms_on_the_member_that_makes_it_ready()
    {
        var g = GroupWith(2);
        g.SchedulePlannedStart(Now.AddDays(10), Now);

        g.AddMember(Guid.NewGuid(), Guid.NewGuid());

        Assert.NotNull(g.ConfirmedAt);
        Assert.Equal(1, Count<GroupConfirmed>(g));
        Assert.Equal(0, Count<MemberJoinedConfirmedGroup>(g));
    }

    [Fact]
    public void Late_member_of_confirmed_group_gets_own_event()
    {
        var g = GroupWith(3);
        g.SchedulePlannedStart(Now.AddDays(10), Now);
        g.ClearDomainEvents();
        var enrollmentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        g.AddMember(enrollmentId, userId);

        var e = Assert.Single(g.DomainEvents.OfType<MemberJoinedConfirmedGroup>());
        Assert.Equal(enrollmentId, e.EnrollmentId);
        Assert.Equal(userId, e.UserId);
        Assert.Equal(0, Count<GroupConfirmed>(g));
    }

    [Fact]
    public void Group_is_confirmed_only_once_even_after_dropping_below_minimum()
    {
        var g = GroupWith(3);
        g.SchedulePlannedStart(Now.AddDays(10), Now);
        var confirmedAt = g.ConfirmedAt;

        g.RemoveMember(g.Members.First().EnrollmentId);
        g.AddMember(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(confirmedAt, g.ConfirmedAt);
        Assert.Equal(1, Count<GroupConfirmed>(g));
    }

    [Fact]
    public void Changing_the_date_raises_rescheduled_with_old_and_new()
    {
        var g = GroupWith(3);
        var first = Now.AddDays(10);
        var second = Now.AddDays(14);
        g.SchedulePlannedStart(first, Now);

        g.SchedulePlannedStart(second, Now);

        var e = Assert.Single(g.DomainEvents.OfType<FirstLessonRescheduled>());
        Assert.Equal(first, e.OldAt);
        Assert.Equal(second, e.NewAt);
        Assert.Equal(second, g.PlannedStartAt);
        Assert.Equal(1, Count<GroupConfirmed>(g));
    }

    [Fact]
    public void Same_date_again_changes_nothing()
    {
        var g = GroupWith(3);
        var at = Now.AddDays(10);
        g.SchedulePlannedStart(at, Now);
        g.ClearDomainEvents();

        g.SchedulePlannedStart(at, Now);

        Assert.Empty(g.DomainEvents);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    [InlineData(366.0)]
    public void Date_outside_allowed_range_is_rejected(double daysFromNow)
    {
        var g = GroupWith(3);
        var now = Now;

        Assert.Throws<GroupsDomainException>(() => g.SchedulePlannedStart(now.AddDays(daysFromNow), now));
        Assert.Null(g.PlannedStartAt);
    }

    [Fact]
    public void Date_cannot_be_set_on_started_group()
    {
        var g = GroupWith(3);
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        for (var i = 0; i < Group.WeeklySlotCount; i++)
            g.AssignProfessorToSlot(i, days[i], new TimeOnly(18, 0), Guid.NewGuid());
        g.Start();

        Assert.Throws<GroupsDomainException>(() => g.SchedulePlannedStart(Now.AddDays(5), Now));
    }

    [Fact]
    public void Date_cannot_be_set_on_dissolved_group()
    {
        var g = GroupWith(1);
        g.Dissolve();

        Assert.Throws<GroupsDomainException>(() => g.SchedulePlannedStart(Now.AddDays(5), Now));
    }

    [Fact]
    public void Individual_and_pair_are_confirmed_with_one_and_two_members()
    {
        var individual = Group.CreateIndividual(Guid.NewGuid());
        individual.AddMember(Guid.NewGuid(), Guid.NewGuid());
        individual.SchedulePlannedStart(Now.AddDays(3), Now);

        var pair = Group.CreatePair(Guid.NewGuid());
        pair.SchedulePlannedStart(Now.AddDays(3), Now);
        pair.AddMember(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(pair.ConfirmedAt);
        pair.AddMember(Guid.NewGuid(), Guid.NewGuid());

        Assert.NotNull(individual.ConfirmedAt);
        Assert.NotNull(pair.ConfirmedAt);
    }
}
