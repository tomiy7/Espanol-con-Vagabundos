namespace GroupsService.Application.Commands.StartGroup;

// Force=true - admin/profesor svesno odlucuje da pokrene grupu i ispod MinSize
// (vidi Group.Start). Default false da se to ne desi slucajno.
//
// UnlockStartsAt - od kada se broje nedelje za otvaranje lekcija (null = trenutak
// starta). Koristi se kad profesor/admin zna da grupa tacno krece kasnije.
public record StartGroupCommand(Guid GroupId, bool Force = false, DateTime? UnlockStartsAt = null);