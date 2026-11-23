using GroupsService.Application.Interfaces;
using GroupsService.Domain.Entities;

namespace GroupsService.Application.Queries.GetAvailableGroups;

public class GetAvailableGroupsHandler
{
    private readonly IGroupRepository _repository;

    public GetAvailableGroupsHandler(IGroupRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<List<GroupSummaryDto>> Handle(GetAvailableGroupsQuery query)
    {
        var groups = await _repository.GetAvailableForCourseAsync(query.CourseId);
        return groups.Select(MapToDto).ToList();
    }

    internal static GroupSummaryDto MapToDto(Group group) => new(
        Id: group.Id,
        CourseId: group.CourseId,
        Type: group.Type,
        Status: group.Status,
        MemberCount: group.MemberCount,
        MinSize: group.MinSize,
        MaxSize: group.MaxSize,
        FormationWindowEndsAt: group.FormationWindowEndsAt,
        Slots: group.Slots
            .OrderBy(s => s.SlotIndex)
            .Select(s => new SlotDto(s.SlotIndex, s.DayOfWeek, s.Time, s.ProfessorId))
            .ToList());
}