namespace GroupsService.Domain.Exceptions;

// Neko je u medjuvremenu izmenio iste podatke (npr. zadnje mesto u grupi) ili je baza
// odbila duplikat. Klijent treba da osvezi podatke i pokusa ponovo (API: 409).
public class ConcurrencyConflictException : GroupsDomainException
{
    public ConcurrencyConflictException(string message) : base(message) { }
}
