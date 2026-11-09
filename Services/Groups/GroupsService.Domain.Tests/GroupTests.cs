using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Domain.Tests;

public class GroupTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);

    private static Group GroupWithSlots(int members)
    {
        var g = Group.CreateGroup(Guid.NewGuid(), 7);
        for (var i = 0; i < members; i++)
            g.AddMember(Guid.NewGuid());

        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        for (var i = 0; i < Group.WeeklySlotCount; i++)
            g.AssignProfessorToSlot(i, days[i], new TimeOnly(18, 0), Guid.NewGuid());

        return g;
    }

    [Fact]
    public void Start_sets_unlock_start_to_start_time_by_default()
    {
        var g = GroupWithSlots(3);
        g.Start(now: Now);

        Assert.Equal(Now, g.StartedAt);
        Assert.Equal(Now, g.UnlockStartsAt);
    }

    [Fact]
    public void Start_accepts_custom_unlock_start()
    {
        var g = GroupWithSlots(3);
        g.Start(unlockStartsAt: Now.AddDays(5), now: Now);

        Assert.Equal(Now.AddDays(5), g.UnlockStartsAt);
    }

    [Fact]
    public void Start_below_minimum_needs_force()
    {
        var g = GroupWithSlots(2);

        Assert.Throws<GroupsDomainException>(() => g.Start(now: Now));
        g.Start(force: true, now: Now);
        Assert.Equal(GroupStatus.Active, g.Status);
    }

    [Fact]
    public void Version_increases_when_members_change()
    {
        var g = Group.CreateGroup(Guid.NewGuid(), 7);
        var before = g.Version;
        var enrollmentId = Guid.NewGuid();

        g.AddMember(enrollmentId);
        g.RemoveMember(enrollmentId);

        Assert.Equal(before + 2, g.Version);
    }

    [Fact]
    public void Removing_member_below_minimum_moves_ready_group_back_to_forming()
    {
        var g = Group.CreateGroup(Guid.NewGuid(), 7);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in ids) g.AddMember(id);
        Assert.Equal(GroupStatus.Ready, g.Status);

        g.RemoveMember(ids[0]);

        Assert.Equal(GroupStatus.Forming, g.Status);
    }

    [Fact]
    public void Dissolve_clears_members_and_only_works_before_start()
    {
        var pair = Group.CreatePair(Guid.NewGuid());
        pair.AddMember(Guid.NewGuid());
        pair.Dissolve();

        Assert.Equal(GroupStatus.Dissolved, pair.Status);
        Assert.Equal(0, pair.MemberCount);

        var active = GroupWithSlots(3);
        active.Start(now: Now);
        Assert.Throws<GroupsDomainException>(() => active.Dissolve());
    }

    [Fact]
    public void Reassigning_an_assigned_slot_requires_admin_permission()
    {
        var g = GroupWithSlots(0);
        var other = Guid.NewGuid();

        var ex = Assert.Throws<GroupsDomainException>(() =>
            g.AssignProfessorToSlot(0, DayOfWeek.Tuesday, new TimeOnly(9, 0), other));
        Assert.Contains("Only an admin", ex.Message);

        g.AssignProfessorToSlot(0, DayOfWeek.Tuesday, new TimeOnly(9, 0), other, allowReassign: true);
        Assert.Equal(other, g.Slots.First(s => s.SlotIndex == 0).ProfessorId);
    }

    [Fact]
    public void Repeating_the_same_assignment_is_always_allowed()
    {
        var g = Group.CreateGroup(Guid.NewGuid(), 7);
        var prof = Guid.NewGuid();

        g.AssignProfessorToSlot(0, DayOfWeek.Monday, new TimeOnly(18, 0), prof);
        g.AssignProfessorToSlot(0, DayOfWeek.Monday, new TimeOnly(18, 0), prof);
    }
}
