namespace GroupsService.Application.Commands.SchedulePlannedStart;

// Planirani datum i vreme prvog casa (UTC). Moze da se menja dok grupa nije pocela.
public record SchedulePlannedStartCommand(Guid GroupId, DateTime PlannedStartAt);
