namespace GroupsService.Application.Commands.StartGroup;

// Force=true - admin/profesor svesno odlucuje da pokrene grupu i ispod MinSize
// (vidi Group.Start). Default false da se to ne desi slucajno.
public record StartGroupCommand(Guid GroupId, bool Force = false);