using GroupsService.Application.Interfaces;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application.Commands.StartGroup;

public class StartGroupHandler
{
    private readonly IGroupRepository _groupRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;

    public StartGroupHandler(
        IGroupRepository groupRepository,
        IEnrollmentRepository enrollmentRepository)
    {
        _groupRepository = groupRepository;
        _enrollmentRepository = enrollmentRepository;
    }

    public async Task Handle(StartGroupCommand command)
    {
        var group = await _groupRepository.GetByIdAsync(command.GroupId)
            ?? throw new GroupsDomainException($"Group with id {command.GroupId} does not exist.");

        group.Start(command.Force, command.UnlockStartsAt);

        // Samo upisi koji cekaju start (Searching/Ready); ostali se preskacu.
        var members = (await _enrollmentRepository.GetByGroupIdAsync(group.Id))
            .Where(m => m.Status is EnrollmentStatus.Searching or EnrollmentStatus.Ready)
            .ToList();
        foreach (var member in members)
            member.MarkActive();

        await _groupRepository.SaveChangesAsync();
        await _enrollmentRepository.SaveChangesAsync();
        
        // TODO notifikacija
    }
}