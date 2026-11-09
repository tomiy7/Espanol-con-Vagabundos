using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Domain.Tests;

public class EnrollmentTests
{
    private static readonly DateTime T0 = new(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc); // ponedeljak

    private static Enrollment NewActive()
    {
        var e = Enrollment.CreateSearching(Guid.NewGuid(), Guid.NewGuid(), EnrollmentType.Group);
        e.MarkReady();
        e.MarkActive(T0);
        return e;
    }

    [Fact]
    public void Freeze_then_unfreeze_accumulates_frozen_time()
    {
        var e = NewActive();
        e.Freeze(Guid.NewGuid(), T0.AddDays(1));
        e.Unfreeze(T0.AddDays(4));

        Assert.Equal(EnrollmentStatus.Active, e.Status);
        Assert.Equal(TimeSpan.FromDays(3), e.TotalFrozenTime);
        Assert.Null(e.FrozenAt);
    }

    [Fact]
    public void Unlocked_lessons_are_three_at_start_and_three_more_each_week()
    {
        var e = NewActive();

        Assert.Equal(3, e.UnlockedLessons(T0, 3, T0));
        Assert.Equal(3, e.UnlockedLessons(T0, 3, T0.AddDays(6)));
        Assert.Equal(6, e.UnlockedLessons(T0, 3, T0.AddDays(7)));
        Assert.Equal(9, e.UnlockedLessons(T0, 3, T0.AddDays(14)));
    }

    [Fact]
    public void Unlocked_lessons_are_zero_before_unlock_start()
    {
        var e = NewActive();
        Assert.Equal(0, e.UnlockedLessons(T0.AddDays(3), 3, T0));
    }

    [Fact]
    public void Frozen_time_pauses_the_unlock_clock()
    {
        var e = NewActive();
        e.Freeze(Guid.NewGuid(), T0.AddDays(3));
        e.Unfreeze(T0.AddDays(10)); // 7 dana zamrznut

        // Proslo 14 dana, od toga 7 zamrznuto -> efektivno 7 dana -> druga nedelja
        Assert.Equal(6, e.UnlockedLessons(T0, 3, T0.AddDays(14)));
    }

    [Fact]
    public void Clock_stays_stopped_while_currently_frozen()
    {
        var e = NewActive();
        e.Freeze(Guid.NewGuid(), T0.AddDays(2));

        Assert.Equal(3, e.UnlockedLessons(T0, 3, T0.AddDays(30)));
    }

    [Fact]
    public void Freeze_expires_after_max_days()
    {
        var e = NewActive();
        e.Freeze(Guid.NewGuid(), T0);

        Assert.False(e.IsFreezeExpired(30, T0.AddDays(30)));
        Assert.True(e.IsFreezeExpired(30, T0.AddDays(31)));
    }

    [Fact]
    public void Cancel_clears_group_and_closes_open_freeze()
    {
        var e = NewActive();
        e.AssignToGroupForTest(Guid.NewGuid());
        e.Freeze(Guid.NewGuid(), DateTime.UtcNow.AddDays(-2));

        e.Cancel();

        Assert.Equal(EnrollmentStatus.Cancelled, e.Status);
        Assert.Null(e.GroupId);
        Assert.Null(e.FrozenAt);
    }

    [Fact]
    public void RevertToSearching_works_only_from_ready()
    {
        var e = Enrollment.CreateSearching(Guid.NewGuid(), Guid.NewGuid(), EnrollmentType.Group);
        Assert.Throws<GroupsDomainException>(() => e.RevertToSearching());

        e.MarkReady();
        e.RevertToSearching();
        Assert.Equal(EnrollmentStatus.Searching, e.Status);
    }

    [Fact]
    public void LeaveGroup_returns_enrollment_to_searching_without_group()
    {
        var e = Enrollment.CreateSearching(Guid.NewGuid(), Guid.NewGuid(), EnrollmentType.Pair);
        e.AssignToGroup(Guid.NewGuid());
        e.MarkReady();

        e.LeaveGroup();

        Assert.Equal(EnrollmentStatus.Searching, e.Status);
        Assert.Null(e.GroupId);
    }
}

internal static class EnrollmentTestExtensions
{
    // Aktivan upis ne moze preko AssignToGroup (traje samo za Searching), pa za test
    // postavljamo GroupId refleksijom.
    public static void AssignToGroupForTest(this Enrollment e, Guid groupId) =>
        typeof(Enrollment).GetProperty(nameof(Enrollment.GroupId))!.SetValue(e, groupId);
}
