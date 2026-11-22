namespace GroupsService.Application.Interfaces;

// Zakljucavanje rasporeda profesora za vreme jedne komande. Provera "profesor nema drugi
// termin u isto vreme" (HasConflictingSlotAsync) i upis termina su dva koraka; bez
// zakljucavanja dva paralelna zahteva za RAZLICITE grupe oba prodju proveru (Version
// grupe tu ne pomaze, jer se menjaju dva razlicita reda). Zakljucavanje drzi baza, ne
// kod, pa vazi i kad API radi u vise instanci.
public interface IProfessorScheduleLock
{
    // Zakljucava raspored datih profesora dok se scope ne potvrdi (CommitAsync) ili ne
    // odbaci (DisposeAsync bez Commit-a ponistava sve sto je u medjuvremenu snimljeno).
    Task<IScheduleLockScope> AcquireAsync(IEnumerable<Guid> professorIds, CancellationToken cancellationToken = default);
}

public interface IScheduleLockScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
