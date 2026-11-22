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

// Pozivi za par: brojac pokusaja i dvostruko prihvatanje.
public class InvitationTests : HandlerTestBase
{
    [Fact]
    public async Task Remaining_partner_can_invite_a_new_partner_and_the_same_person_again()
    {
        var inviter = Guid.NewGuid();
        var invitee = Guid.NewGuid();
        await Invite().Handle(new InvitePartnerCommand(inviter, invitee, CourseId));
        var accepted = await Accept().Handle(new AcceptInvitationCommand(Invitations.Items.Single().Token));
        var inviteeEnrollment = Enrollments.Items.Single(e => e.Id == accepted.InviteeEnrollmentId);
        await Cancel().Handle(new CancelEnrollmentCommand(inviteeEnrollment.Id, invitee));

        // novi partner
        await Invite().Handle(new InvitePartnerCommand(inviter, Guid.NewGuid(), CourseId));
        // isti partner koji je otisao: prihvacen poziv se ne racuna u pokusaje
        await Invite().Handle(new InvitePartnerCommand(inviter, invitee, CourseId));

        Assert.Equal(3, Invitations.Items.Count);
        Assert.Equal(1, Invitations.Items.Last().AttemptNumber); // brojac krece ispocetka
    }

    [Fact]
    public async Task Second_accept_from_a_double_invitation_is_rejected_with_a_clear_message()
    {
        var inviter = Guid.NewGuid();
        await Invite().Handle(new InvitePartnerCommand(inviter, Guid.NewGuid(), CourseId));
        await Invite().Handle(new InvitePartnerCommand(inviter, Guid.NewGuid(), CourseId));
        var tokens = Invitations.Items.Select(i => i.Token).ToList();

        await Accept().Handle(new AcceptInvitationCommand(tokens[0]));
        var ex = await Assert.ThrowsAsync<GroupsDomainException>(() =>
            Accept().Handle(new AcceptInvitationCommand(tokens[1])));

        Assert.Contains("no longer looking for a partner", ex.Message);
        Assert.Single(Groups.Items);
    }
}
