using GroupsService.Application.Interfaces;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace GroupsService.Application.Commands.JoinGroup;

// Ulazak u POSTOJECU grupu (Group tip). Isti handler koriste profesor/admin kad
// ubacuju ucenika, i ucenik kad sam bira termin - razlikuje ih samo AddedByStaff.
public class JoinGroupHandler
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly ILogger<JoinGroupHandler> _logger;

    public JoinGroupHandler(
        IEnrollmentRepository enrollmentRepository,
        IGroupRepository groupRepository,
        ILogger<JoinGroupHandler> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _groupRepository = groupRepository;
        _logger = logger;
    }

    public async Task<JoinGroupResult> Handle(JoinGroupCommand command)
    {
        var group = await _groupRepository.GetByIdAsync(command.GroupId)
            ?? throw new GroupsDomainException($"Group with id {command.GroupId} does not exist.");

        if (group.Type != GroupType.Group)
            throw new GroupsDomainException("Only open groups can be joined directly.");

        // Rok formiranja vazi za samoprijavu; profesor/admin sme da doda i kasnije.
        if (!command.AddedByStaff && group.FormationWindowExpired)
            throw new GroupsDomainException("Enrollment for this group has closed.");

        // Isto pravilo kao u ostalim upisima: blokira i aktivan i ZAVRSEN upis na kurs.
        var existing = await _enrollmentRepository.GetNonCancelledAsync(command.UserId, group.CourseId);
        if (existing is not null)
            throw EnrollmentGuard.Blocked(existing, self: !command.AddedByStaff);

        var enrollment = Enrollment.CreateSearching(command.UserId, group.CourseId, EnrollmentType.Group);

        var wasReadyBefore = group.Status == GroupStatus.Ready;
        group.AddMember(enrollment.Id); // baca ako je grupa puna ili nije otvorena
        enrollment.AssignToGroup(group.Id);

        // Ulazak u grupu koja je vec Ready znaci da je i novi upis Ready. Kad grupa
        // TEK SAD postane Ready, svi ostali clanovi prelaze u Ready zajedno sa njim
        // (novi upis jos nije u bazi pa ga upit ne vraca - zato ga ovde izuzimamo).
        var becameReadyNow = false;
        if (group.Status == GroupStatus.Ready)
        {
            enrollment.MarkReady();

            if (!wasReadyBefore)
            {
                becameReadyNow = true;
                var others = await _enrollmentRepository.GetByGroupIdAsync(group.Id);
                foreach (var member in others.Where(m => m.Id != enrollment.Id))
                    member.MarkReady();
            }
        }

        await _enrollmentRepository.AddAsync(enrollment);
        await _enrollmentRepository.SaveChangesAsync();
        await _groupRepository.SaveChangesAsync();
        
        //TODO notifikacija

        return new JoinGroupResult(enrollment.Id, group.Id);
    }
}
