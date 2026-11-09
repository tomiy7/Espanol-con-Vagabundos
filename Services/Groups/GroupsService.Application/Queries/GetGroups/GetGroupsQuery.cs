using GroupsService.Domain.Enums;

namespace GroupsService.Application.Queries.GetGroups;

// Svi filteri su opcioni. ProfessorId vraca grupe u kojima taj profesor drzi bar jedan
// termin - "moje grupe", a za Individual (Type=Individual, Status=Ready) i zahteve koji
// cekaju da profesor potvrdi termin.
public record GetGroupsQuery(Guid? CourseId, GroupStatus? Status, GroupType? Type, Guid? ProfessorId);