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

// Zajednicka priprema za testove handlera: in-memory repozitorijumi (Fakes.cs), fabrike
// handlera i scenariji (otvorena grupa, upis u grupu, individualni upis, zavrsen kurs).
// Svaki test dobija svoju instancu (xUnit pravi novu klasu po testu), pa nema deljenog stanja.
public abstract class HandlerTestBase
{
    protected readonly FakeEnrollmentRepository Enrollments = new();
    protected readonly FakeGroupRepository Groups = new();
    protected readonly FakeInvitationRepository Invitations = new();
    protected readonly IOptions<EnrollmentOptions> Settings = Microsoft.Extensions.Options.Options.Create(new EnrollmentOptions());
    protected readonly Guid CourseId = Guid.NewGuid();

    // Mejlove salje Notifications servis na osnovu dogadjaja (outbox), ne handleri.
    protected CancelEnrollmentHandler Cancel() => new(Enrollments, Groups);

    protected JoinGroupHandler Join() => new(Enrollments, Groups, NullLogger<JoinGroupHandler>.Instance);

    protected StartGroupHandler Start() => new(Groups, Enrollments);

    protected InvitePartnerHandler Invite() => new(Enrollments, Invitations, Settings);

    protected AcceptInvitationHandler Accept() => new(Invitations, Enrollments, Groups);

    protected CourseCompletedHandler CourseDone() => new(Enrollments, Groups,
        NullLogger<CourseCompletedHandler>.Instance);

    // Otvorena Group grupa sa 3 dodeljena slota, spremna za start.
    protected Group OpenGroup()
    {
        var g = Group.CreateGroup(CourseId, 7);
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        for (var i = 0; i < 3; i++)
            g.AssignProfessorToSlot(i, days[i], new TimeOnly(18, 0), Guid.NewGuid());
        Groups.Items.Add(g);
        return g;
    }

    protected async Task<Enrollment> JoinUser(Group g)
    {
        var userId = Guid.NewGuid();
        var result = await Join().Handle(new JoinGroupCommand(userId, g.Id, AddedByStaff: true));
        return Enrollments.Items.Single(e => e.Id == result.EnrollmentId);
    }

    protected async Task<Enrollment> CreateIndividual()
    {
        await new CreateIndividualEnrollmentHandler(Enrollments, Groups)
            .Handle(new CreateIndividualEnrollmentCommand(Guid.NewGuid(), CourseId, Guid.NewGuid()));

        return Enrollments.Items.Single();
    }

    protected async Task FinishCourse(Enrollment e)
    {
        await CourseDone().Handle(new CourseCompletedCommand(e.Id, e.UserId, e.CourseId));
    }
}
