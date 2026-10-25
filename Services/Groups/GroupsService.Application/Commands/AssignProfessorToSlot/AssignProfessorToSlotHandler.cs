using GroupsService.Application.Interfaces;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application.Commands.AssignProfessorToSlot;

public class AssignProfessorToSlotHandler
{
    private readonly IGroupRepository _groupRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;

    public AssignProfessorToSlotHandler(
        IGroupRepository groupRepository,
        IEnrollmentRepository enrollmentRepository)
    {
        _groupRepository = groupRepository;
        _enrollmentRepository = enrollmentRepository;
    }
    
    public async Task Handle(AssignProfessorToSlotCommand command)
    {
        var group = await _groupRepository.GetByIdAsync(command.GroupId)
                    ?? throw new GroupsDomainException($"Group with id {command.GroupId} does not exist.");

        // Nivo 1 zastita od duplog zakazivanja - vidi komentar na HasConflictingSlotAsync.
        // NAPOMENA: ovo je provera na DayOfWeek+Time pravilu, ne na konkretnom datumu -
        // ako profesor ima termin ponedeljkom u 17h u Grupi A, ne moze ni u Grupi B,
        // zauvek (nema koncepta "samo ova nedelja slobodna/zauzeta"). Vidi Slot.cs.
        var hasConflict = await _groupRepository.HasConflictingSlotAsync(
            command.ProfessorId, command.DayOfWeek, command.Time, command.GroupId);

        if (hasConflict)
            throw new GroupsDomainException(
                $"Professor already has a conflicting slot on {command.DayOfWeek} at {command.Time} in another group.");

        group.AssignProfessorToSlot(command.SlotIndex, command.DayOfWeek, command.Time, command.ProfessorId);
        await _groupRepository.SaveChangesAsync();
        
        // TODO notifikacija
    }

}