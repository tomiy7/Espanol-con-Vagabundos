using GroupsService.Application.Interfaces;
using GroupsService.Application.Options;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace GroupsService.Application.Commands.CreateGroup;

// Grupu (Group tip) unapred otvara profesor/admin sa gotovim rasporedom, npr.
// ponedeljak/sreda/petak ili utorak/cetvrtak/subota. Ucenike u nju ubacuje
// profesor/admin (JoinGroup, AddedByStaff) ili kasnije sam ucenik biranjem termina.
public class CreateGroupHandler
{
    private readonly IGroupRepository _groupRepository;
    private readonly EnrollmentOptions _options;

    public CreateGroupHandler(IGroupRepository groupRepository, IOptions<EnrollmentOptions> options)
    {
        _groupRepository = groupRepository;
        _options = options.Value;
    }

    public async Task<CreateGroupResult> Handle(CreateGroupCommand command)
    {
        if (command.Slots.Count != Group.WeeklySlotCount)
            throw new GroupsDomainException($"A group needs exactly {Group.WeeklySlotCount} weekly slots.");

        // Dva termina u isto dan+vreme u istoj grupi nemaju smisla.
        var distinct = command.Slots.Select(s => (s.DayOfWeek, s.Time)).Distinct().Count();
        if (distinct != command.Slots.Count)
            throw new GroupsDomainException("Each slot must have a different day and time.");

        if (!command.IsAdmin && command.Slots.Any(s => s.ProfessorId != command.RequestingUserId))
            throw new GroupsDomainException(
                "Professors can only create groups with themselves as the professor. Ask an admin to assign other professors.");

        // NAPOMENA: ne proveravamo da ProfessorId stvarno ima professor rolu u Auth-u -
        // ostaje tech debt, isto kao kod Individual upisa.
        var group = Group.CreateGroup(command.CourseId, command.FormationWindowDays ?? _options.GroupFormationWindowDays);

        for (var i = 0; i < command.Slots.Count; i++)
        {
            var slot = command.Slots[i];

            // Nivo 1 zastita od duplog zakazivanja - isto pravilo kao u AssignProfessorToSlot.
            var hasConflict = await _groupRepository.HasConflictingSlotAsync(
                slot.ProfessorId, slot.DayOfWeek, slot.Time, group.Id);

            if (hasConflict)
                throw new GroupsDomainException(
                    $"Professor already has a conflicting slot on {slot.DayOfWeek} at {slot.Time} in another group.");

            group.AssignProfessorToSlot(i, slot.DayOfWeek, slot.Time, slot.ProfessorId);
        }

        await _groupRepository.AddAsync(group);
        await _groupRepository.SaveChangesAsync();

        return new CreateGroupResult(group.Id);
    }
}
