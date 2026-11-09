using GroupsService.Application.Commands.CancelEnrollment;
using GroupsService.Application.Commands.InvitePartner;
using GroupsService.Application.Commands.JoinGroup;
using GroupsService.Application.Commands.RecordLessonCompleted;
using GroupsService.Application.Commands.RespondToInvitation;
using GroupsService.Application.Commands.StartGroup;
using GroupsService.Application.Interfaces;
using GroupsService.Application.Options;
using GroupsService.Application.Queries.CheckAccess;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GroupsService.Application.Tests;

public class HandlerTests
{
    private readonly FakeEnrollmentRepository _enrollments = new();
    private readonly FakeGroupRepository _groups = new();
    private readonly FakeInvitationRepository _invitations = new();
    private readonly IUserDirectory _users = Substitute.For<IUserDirectory>();
    private readonly ICourseDirectory _courses = Substitute.For<ICourseDirectory>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IOptions<EnrollmentOptions> _options = Microsoft.Extensions.Options.Options.Create(new EnrollmentOptions());
    private readonly Guid _courseId = Guid.NewGuid();

    private CancelEnrollmentHandler Cancel() => new(_enrollments, _groups);

    private JoinGroupHandler Join() => new(_enrollments, _groups, _users, _courses, _notifications,
        NullLogger<JoinGroupHandler>.Instance);

    private StartGroupHandler Start() => new(_groups, _enrollments, _users, _courses, _notifications,
        NullLogger<StartGroupHandler>.Instance);

    private InvitePartnerHandler Invite() => new(_enrollments, _invitations, _users, _courses, _notifications,
        _options, NullLogger<InvitePartnerHandler>.Instance);

    private AcceptInvitationHandler Accept() => new(_invitations, _enrollments, _groups, _users, _courses,
        _notifications, NullLogger<AcceptInvitationHandler>.Instance);

    private RecordLessonCompletedHandler Record() => new(_enrollments, _groups,
        NullLogger<RecordLessonCompletedHandler>.Instance);

    // Otvorena Group grupa sa 3 dodeljena slota, spremna za start.
    private Group OpenGroup()
    {
        var g = Group.CreateGroup(_courseId, 7);
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        for (var i = 0; i < 3; i++)
            g.AssignProfessorToSlot(i, days[i], new TimeOnly(18, 0), Guid.NewGuid());
        _groups.Items.Add(g);
        return g;
    }

    private async Task<Enrollment> JoinUser(Group g)
    {
        var userId = Guid.NewGuid();
        var result = await Join().Handle(new JoinGroupCommand(userId, g.Id, AddedByStaff: true));
        return _enrollments.Items.Single(e => e.Id == result.EnrollmentId);
    }

    // ---------- otkazivanje ----------

    [Fact]
    public async Task Cancel_from_ready_group_sends_remaining_members_back_to_searching()
    {
        var g = OpenGroup();
        var e1 = await JoinUser(g);
        var e2 = await JoinUser(g);
        var e3 = await JoinUser(g);
        Assert.Equal(GroupStatus.Ready, g.Status);
        Assert.All(new[] { e1, e2, e3 }, e => Assert.Equal(EnrollmentStatus.Ready, e.Status));

        await Cancel().Handle(new CancelEnrollmentCommand(e3.Id, e3.UserId));

        Assert.Equal(GroupStatus.Forming, g.Status);
        Assert.Equal(EnrollmentStatus.Searching, e1.Status);
        Assert.Equal(EnrollmentStatus.Searching, e2.Status);
        Assert.Equal(EnrollmentStatus.Cancelled, e3.Status);
    }

    [Fact]
    public async Task New_member_can_join_after_group_dropped_back_to_forming()
    {
        var g = OpenGroup();
        var e1 = await JoinUser(g);
        await JoinUser(g);
        var e3 = await JoinUser(g);
        await Cancel().Handle(new CancelEnrollmentCommand(e3.Id, e3.UserId));

        var e4 = await JoinUser(g); // ranije je pucalo na MarkReady()

        Assert.Equal(GroupStatus.Ready, g.Status);
        Assert.Equal(EnrollmentStatus.Ready, e1.Status);
        Assert.Equal(EnrollmentStatus.Ready, e4.Status);
    }

    [Fact]
    public async Task Start_works_after_a_member_cancelled()
    {
        var g = OpenGroup();
        await JoinUser(g);
        await JoinUser(g);
        var e3 = await JoinUser(g);
        var e4 = await JoinUser(g);
        await Cancel().Handle(new CancelEnrollmentCommand(e4.Id, e4.UserId));

        await Start().Handle(new StartGroupCommand(g.Id)); // ranije je pucalo na otkazanom upisu

        Assert.Equal(GroupStatus.Active, g.Status);
        Assert.Equal(EnrollmentStatus.Active, e3.Status);
        Assert.Equal(EnrollmentStatus.Cancelled, e4.Status);
    }

    [Fact]
    public async Task Start_sets_unlock_start_from_command()
    {
        var g = OpenGroup();
        await JoinUser(g);
        await JoinUser(g);
        await JoinUser(g);
        var unlock = DateTime.UtcNow.AddDays(10);

        await Start().Handle(new StartGroupCommand(g.Id, UnlockStartsAt: unlock));

        Assert.Equal(unlock, g.UnlockStartsAt);
    }

    [Fact]
    public async Task Cancelling_pair_member_dissolves_group_and_frees_the_partner()
    {
        var inviter = Guid.NewGuid();
        var invitee = Guid.NewGuid();
        await Invite().Handle(new InvitePartnerCommand(inviter, invitee, _courseId));
        var token = _invitations.Items.Single().Token;
        var accepted = await Accept().Handle(new AcceptInvitationCommand(token));

        var inviteeEnrollment = _enrollments.Items.Single(e => e.Id == accepted.InviteeEnrollmentId);
        var inviterEnrollment = _enrollments.Items.Single(e => e.UserId == inviter);
        var group = _groups.Items.Single();

        await Cancel().Handle(new CancelEnrollmentCommand(inviteeEnrollment.Id, invitee));

        Assert.Equal(GroupStatus.Dissolved, group.Status);
        Assert.Equal(EnrollmentStatus.Searching, inviterEnrollment.Status);
        Assert.Null(inviterEnrollment.GroupId);
    }

    [Fact]
    public async Task Remaining_partner_can_invite_a_new_partner_and_the_same_person_again()
    {
        var inviter = Guid.NewGuid();
        var invitee = Guid.NewGuid();
        await Invite().Handle(new InvitePartnerCommand(inviter, invitee, _courseId));
        var accepted = await Accept().Handle(new AcceptInvitationCommand(_invitations.Items.Single().Token));
        var inviteeEnrollment = _enrollments.Items.Single(e => e.Id == accepted.InviteeEnrollmentId);
        await Cancel().Handle(new CancelEnrollmentCommand(inviteeEnrollment.Id, invitee));

        // novi partner
        await Invite().Handle(new InvitePartnerCommand(inviter, Guid.NewGuid(), _courseId));
        // isti partner koji je otisao: prihvacen poziv se ne racuna u pokusaje
        await Invite().Handle(new InvitePartnerCommand(inviter, invitee, _courseId));

        Assert.Equal(3, _invitations.Items.Count);
        Assert.Equal(1, _invitations.Items.Last().AttemptNumber); // brojac krece ispocetka
    }

    [Fact]
    public async Task Cancelling_individual_before_start_dissolves_the_private_group()
    {
        var e = await CreateIndividual();
        var group = _groups.Items.Single();

        await Cancel().Handle(new CancelEnrollmentCommand(e.Id, e.UserId));

        Assert.Equal(GroupStatus.Dissolved, group.Status);
        Assert.Equal(EnrollmentStatus.Cancelled, e.Status);
    }

    [Fact]
    public async Task Cancelling_individual_after_start_closes_the_empty_group()
    {
        var e = await CreateIndividual();
        var group = _groups.Items.Single();
        group.AssignProfessorToSlot(0, DayOfWeek.Tuesday, new TimeOnly(17, 0), e.ProfessorId!.Value);
        await Start().Handle(new StartGroupCommand(group.Id));
        Assert.Equal(GroupStatus.Active, group.Status);

        await Cancel().Handle(new CancelEnrollmentCommand(e.Id, e.UserId));

        Assert.Equal(GroupStatus.Completed, group.Status); // vise ne drzi termin profesora
    }

    [Fact]
    public async Task Cancelling_someone_elses_enrollment_is_rejected_unless_staff()
    {
        var g = OpenGroup();
        var e1 = await JoinUser(g);

        await Assert.ThrowsAsync<GroupsDomainException>(() =>
            Cancel().Handle(new CancelEnrollmentCommand(e1.Id, Guid.NewGuid())));

        await Cancel().Handle(new CancelEnrollmentCommand(e1.Id, Guid.NewGuid(), IsStaff: true));
        Assert.Equal(EnrollmentStatus.Cancelled, e1.Status);
    }

    // ---------- pozivi ----------

    [Fact]
    public async Task Second_accept_from_a_double_invitation_is_rejected_with_a_clear_message()
    {
        var inviter = Guid.NewGuid();
        await Invite().Handle(new InvitePartnerCommand(inviter, Guid.NewGuid(), _courseId));
        await Invite().Handle(new InvitePartnerCommand(inviter, Guid.NewGuid(), _courseId));
        var tokens = _invitations.Items.Select(i => i.Token).ToList();

        await Accept().Handle(new AcceptInvitationCommand(tokens[0]));
        var ex = await Assert.ThrowsAsync<GroupsDomainException>(() =>
            Accept().Handle(new AcceptInvitationCommand(tokens[1])));

        Assert.Contains("no longer looking for a partner", ex.Message);
        Assert.Single(_groups.Items);
    }

    // ---------- napredak ----------

    [Fact]
    public async Task Group_auto_completes_ignoring_frozen_members()
    {
        var g = OpenGroup();
        var e1 = await JoinUser(g);
        var e2 = await JoinUser(g);
        var e3 = await JoinUser(g);
        await Start().Handle(new StartGroupCommand(g.Id));
        e3.Freeze(Guid.NewGuid());

        await FinishCourse(e1);
        Assert.Equal(GroupStatus.Active, g.Status); // e2 jos nije gotov
        await FinishCourse(e2);

        Assert.Equal(GroupStatus.Completed, g.Status);
        Assert.Equal(EnrollmentStatus.Frozen, e3.Status);
    }

    // ---------- pristup i otvaranje lekcija ----------

    [Fact]
    public async Task CheckAccess_returns_unlocked_lessons_from_group_start()
    {
        var g = OpenGroup();
        var e1 = await JoinUser(g);
        await JoinUser(g);
        await JoinUser(g);
        await Start().Handle(new StartGroupCommand(g.Id, UnlockStartsAt: DateTime.UtcNow.AddDays(-15)));

        var handler = new CheckAccessHandler(_enrollments, _groups, _options);
        var result = await handler.Handle(new CheckAccessQuery(e1.UserId, _courseId));

        Assert.True(result.HasAccess);
        Assert.Equal(e1.Id, result.EnrollmentId);
        Assert.Equal(9, result.UnlockedLessons); // 15 dana = 2 pune nedelje -> 3 * (2 + 1)
    }

    [Fact]
    public async Task CheckAccess_denies_a_frozen_enrollment()
    {
        var g = OpenGroup();
        var e1 = await JoinUser(g);
        await JoinUser(g);
        await JoinUser(g);
        await Start().Handle(new StartGroupCommand(g.Id));
        e1.Freeze(Guid.NewGuid());

        var result = await new CheckAccessHandler(_enrollments, _groups, _options)
            .Handle(new CheckAccessQuery(e1.UserId, _courseId));

        Assert.False(result.HasAccess);
        Assert.Equal(0, result.UnlockedLessons);
    }

    // ---------- pomocno ----------

    private async Task<Enrollment> CreateIndividual()
    {
        var handler = new Commands.CreateIndividualEnrollment.CreateIndividualEnrollmentHandler(
            _enrollments, _groups, _users, _courses, _notifications,
            NullLogger<Commands.CreateIndividualEnrollment.CreateIndividualEnrollmentHandler>.Instance);

        await handler.Handle(new Commands.CreateIndividualEnrollment.CreateIndividualEnrollmentCommand(
            Guid.NewGuid(), _courseId, Guid.NewGuid()));

        return _enrollments.Items.Single();
    }

    private async Task FinishCourse(Enrollment e)
    {
        await Record().Handle(new RecordLessonCompletedCommand(e.UserId, e.CourseId, e.Id, Guid.NewGuid(), TotalLessons: 1));
    }
}
