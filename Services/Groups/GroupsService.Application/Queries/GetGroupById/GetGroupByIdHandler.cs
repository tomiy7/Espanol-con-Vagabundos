using GroupsService.Application.Interfaces;
using GroupsService.Application.Queries.GetAvailableGroups;

namespace GroupsService.Application.Queries.GetGroupById;

public class GetGroupByIdHandler
{
    private readonly IGroupRepository _groupRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IUserDirectory _userDirectory;

    public GetGroupByIdHandler(
        IGroupRepository groupRepository,
        IEnrollmentRepository enrollmentRepository,
        IUserDirectory userDirectory)
    {
        _groupRepository = groupRepository;
        _enrollmentRepository = enrollmentRepository;
        _userDirectory = userDirectory;
    }
    
    public async Task<GroupDetailDto?> Handle(GetGroupByIdQuery query)
    {
        var group = await _groupRepository.GetByIdAsync(query.GroupId);
        if (group == null) return null;

        var enrollments = await _enrollmentRepository.GetByGroupIdAsync(group.Id);

        // Grupa ima najvise 6 clanova, pa je poziv Auth-a po clanu prihvatljiv.
        // Ime je best-effort - nedostupan Auth ne sme da obori pregled grupe.
        var members = new List<GroupMemberDto>();
        foreach (var enrollment in enrollments.OrderBy(e => e.CreatedAt))
        {
            var contact = await _userDirectory.GetUserContactAsync(enrollment.UserId);
            members.Add(new GroupMemberDto(
                enrollment.Id, enrollment.UserId, contact?.FullName,
                enrollment.Status));
        }

        return new GroupDetailDto(
            Id: group.Id,
            CourseId: group.CourseId,
            Type: group.Type,
            Status: group.Status,
            MinSize: group.MinSize,
            MaxSize: group.MaxSize,
            FormationWindowEndsAt: group.FormationWindowEndsAt,
            StartedAt: group.StartedAt,
            PlannedStartAt: group.PlannedStartAt,
            ConfirmedAt: group.ConfirmedAt,
            Slots: group.Slots
                .OrderBy(s => s.SlotIndex)
                .Select(s => new SlotDto(s.SlotIndex, s.DayOfWeek, s.Time, s.ProfessorId))
                .ToList(),
            Members: members);
    }
}