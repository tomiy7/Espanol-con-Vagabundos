using GroupsService.Application.Interfaces;
using GroupsService.Domain.Exceptions;

namespace GroupsService.Application.Commands.AssignProfessorToSlot;

public class AssignProfessorToSlotHandler
{
    private readonly IGroupRepository _groupRepository;
    private readonly IProfessorScheduleLock _scheduleLock;

    public AssignProfessorToSlotHandler(
        IGroupRepository groupRepository,
        IProfessorScheduleLock scheduleLock)
    {
        _groupRepository = groupRepository;
        _scheduleLock = scheduleLock;
    }
    
    public async Task Handle(AssignProfessorToSlotCommand command)
    {
        var group = await _groupRepository.GetByIdAsync(command.GroupId)
            ?? throw EntityNotFoundException.Group(command.GroupId);

        // Provera konflikta i upis termina moraju biti atomicni za ovog profesora.
        await using var scheduleLock = await _scheduleLock.AcquireAsync([command.ProfessorId]);

        // Nivo 1 zastita od duplog zakazivanja - vidi komentar na HasConflictingSlotAsync.
        // NAPOMENA: ovo je provera na DayOfWeek+Time pravilu, ne na konkretnom datumu -
        // ako profesor ima termin ponedeljkom u 17h u Grupi A, ne moze ni u Grupi B,
        // zauvek (nema koncepta "samo ova nedelja slobodna/zauzeta"). Vidi Slot.cs.
        var hasConflict = await _groupRepository.HasConflictingSlotAsync(
            command.ProfessorId, command.DayOfWeek, command.Time, command.GroupId);

        if (hasConflict)
            throw new GroupsDomainException(
                $"Professor already has a conflicting slot on {command.DayOfWeek} at {command.Time} in another group.");

        group.AssignProfessorToSlot(
            command.SlotIndex, command.DayOfWeek, command.Time, command.ProfessorId, allowReassign: command.AllowReassign);
        await _groupRepository.SaveChangesAsync();
        await scheduleLock.CommitAsync();
        
    }

}