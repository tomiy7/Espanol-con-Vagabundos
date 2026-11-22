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

// Pristup kursu i broj otvorenih lekcija.
public class CheckAccessTests : HandlerTestBase
{
    [Fact]
    public async Task CheckAccess_returns_unlocked_lessons_from_group_start()
    {
        var g = OpenGroup();
        var e1 = await JoinUser(g);
        await JoinUser(g);
        await JoinUser(g);
        await Start().Handle(new StartGroupCommand(g.Id, UnlockStartsAt: DateTime.UtcNow.AddDays(-15)));

        var handler = new CheckAccessHandler(Enrollments, Groups, Settings);
        var result = await handler.Handle(new CheckAccessQuery(e1.UserId, CourseId));

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

        var result = await new CheckAccessHandler(Enrollments, Groups, Settings)
            .Handle(new CheckAccessQuery(e1.UserId, CourseId));

        Assert.False(result.HasAccess);
        Assert.Equal(0, result.UnlockedLessons);
    }
}
