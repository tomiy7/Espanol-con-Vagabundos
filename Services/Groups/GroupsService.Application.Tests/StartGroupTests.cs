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

// Start grupe: preskace otkazane clanove i postavlja pocetak otvaranja lekcija.
public class StartGroupTests : HandlerTestBase
{
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
}
