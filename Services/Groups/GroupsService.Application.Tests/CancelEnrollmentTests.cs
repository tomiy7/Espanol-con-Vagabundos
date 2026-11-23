using GroupsService.Application.Commands.CancelEnrollment;
using GroupsService.Application.Commands.CourseCompleted;
using GroupsService.Application.Commands.CreateIndividualEnrollment;
using GroupsService.Application.Commands.InvitePartner;
using GroupsService.Application.Commands.JoinGroup;
using GroupsService.Application.Commands.RespondToInvitation;
using GroupsService.Application.Commands.StartGroup;
using GroupsService.Application.Options;
using GroupsService.Application.Queries.CheckAccess;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GroupsService.Application.Tests;

// Otkazivanje: posledice po grupu (Ready -> Forming, rasformiranje Pair/Individual, zatvaranje prazne aktivne grupe) i prava.
public class CancelEnrollmentTests : HandlerTestBase
{
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
    public async Task Cancelling_pair_member_dissolves_group_and_frees_the_partner()
    {
        var inviter = Guid.NewGuid();
        var invitee = Guid.NewGuid();
        await Invite().Handle(new InvitePartnerCommand(inviter, invitee, CourseId));
        var token = Invitations.Items.Single().Token;
        var accepted = await Accept().Handle(new AcceptInvitationCommand(token));

        var inviteeEnrollment = Enrollments.Items.Single(e => e.Id == accepted.InviteeEnrollmentId);
        var inviterEnrollment = Enrollments.Items.Single(e => e.UserId == inviter);
        var group = Groups.Items.Single();

        await Cancel().Handle(new CancelEnrollmentCommand(inviteeEnrollment.Id, invitee));

        Assert.Equal(GroupStatus.Dissolved, group.Status);
        Assert.Equal(EnrollmentStatus.Searching, inviterEnrollment.Status);
        Assert.Null(inviterEnrollment.GroupId);
    }

    [Fact]
    public async Task Cancelling_individual_before_start_dissolves_the_private_group()
    {
        var e = await CreateIndividual();
        var group = Groups.Items.Single();

        await Cancel().Handle(new CancelEnrollmentCommand(e.Id, e.UserId));

        Assert.Equal(GroupStatus.Dissolved, group.Status);
        Assert.Equal(EnrollmentStatus.Cancelled, e.Status);
    }

    [Fact]
    public async Task Cancelling_individual_after_start_closes_the_empty_group()
    {
        var e = await CreateIndividual();
        var group = Groups.Items.Single();
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
}
