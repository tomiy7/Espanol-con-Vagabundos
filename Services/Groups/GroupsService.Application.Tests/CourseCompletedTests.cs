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

// Zavrsen kurs (CourseCompleted iz Courses): idempotentnost i automatsko zatvaranje grupe.
public class CourseCompletedTests : HandlerTestBase
{
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

    [Fact]
    public async Task CourseCompleted_is_idempotent_and_ignores_mismatched_or_inactive_enrollments()
    {
        var g = OpenGroup();
        var e1 = await JoinUser(g);
        await JoinUser(g);
        await JoinUser(g);

        // jos nije Active (Ready): ignorise se
        await FinishCourse(e1);
        Assert.Equal(EnrollmentStatus.Ready, e1.Status);

        await Start().Handle(new StartGroupCommand(g.Id));

        // tudj korisnik: ignorise se
        await CourseDone().Handle(new CourseCompletedCommand(e1.Id, Guid.NewGuid(), e1.CourseId));
        Assert.Equal(EnrollmentStatus.Active, e1.Status);

        // nepostojeci upis: ne baca izuzetak
        await CourseDone().Handle(new CourseCompletedCommand(Guid.NewGuid(), e1.UserId, e1.CourseId));

        await FinishCourse(e1);
        var completedAt = e1.CompletedAt;
        await FinishCourse(e1); // duplikat poruke ne menja nista
        Assert.Equal(EnrollmentStatus.Completed, e1.Status);
        Assert.Equal(completedAt, e1.CompletedAt);
    }

    [Fact]
    public async Task Group_completes_when_the_last_remaining_member_finishes()
    {
        var g = OpenGroup();
        var e1 = await JoinUser(g);
        var e2 = await JoinUser(g);
        var e3 = await JoinUser(g);
        await Start().Handle(new StartGroupCommand(g.Id));
        await Cancel().Handle(new CancelEnrollmentCommand(e3.Id, e3.UserId));

        await FinishCourse(e1);
        Assert.Equal(GroupStatus.Active, g.Status);
        await FinishCourse(e2);

        Assert.Equal(GroupStatus.Completed, g.Status);
    }
}
