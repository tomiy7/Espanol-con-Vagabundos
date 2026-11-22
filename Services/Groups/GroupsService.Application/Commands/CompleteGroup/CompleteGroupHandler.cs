using GroupsService.Application.Interfaces;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application.Commands.CompleteGroup;

// Rucno zatvaranje grupe (admin/profesor) - za slucaj kad se grupa ne zatvori
// automatski (npr. neko je zamrznut ili nikad nije zavrsio, a ostali jesu).
// Aktivni clanovi se zavrsavaju zajedno sa grupom; Frozen/Cancelled se ne diraju
// (zamrznut upis zadrzava svoj napredak i moze se nastaviti u drugoj grupi).
public class CompleteGroupHandler
{
    private readonly IGroupRepository _groupRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;

    public CompleteGroupHandler(IGroupRepository groupRepository, IEnrollmentRepository enrollmentRepository)
    {
        _groupRepository = groupRepository;
        _enrollmentRepository = enrollmentRepository;
    }

    public async Task Handle(CompleteGroupCommand command)
    {
        var group = await _groupRepository.GetByIdAsync(command.GroupId)
            ?? throw EntityNotFoundException.Group(command.GroupId);

        group.Complete();

        var members = await _enrollmentRepository.GetByGroupIdAsync(group.Id);
        foreach (var member in members.Where(m => m.Status == EnrollmentStatus.Active))
            member.Complete();

        await _groupRepository.SaveChangesAsync();
        await _enrollmentRepository.SaveChangesAsync();
    }
}
