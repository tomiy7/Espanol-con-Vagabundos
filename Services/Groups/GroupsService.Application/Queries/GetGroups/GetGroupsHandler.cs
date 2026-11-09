using GroupsService.Application.Interfaces;
using GroupsService.Application.Queries.GetAvailableGroups;

namespace GroupsService.Application.Queries.GetGroups;

public class GetGroupsHandler
{
    private readonly IGroupRepository _repository;

    public GetGroupsHandler(IGroupRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<List<GroupSummaryDto>> Handle(GetGroupsQuery query)
    {
        var groups = await _repository.GetListAsync(query.CourseId, query.Status, query.Type, query.ProfessorId);
        return groups.Select(GetAvailableGroupsHandler.MapToDto).ToList();
    }
}