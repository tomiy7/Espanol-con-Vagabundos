using GroupsService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GroupsService.Infrastructure.Data;

// Postgres advisory lock po profesoru (pg_advisory_xact_lock) unutar transakcije
// DbContext-a. Drugi zahtev za istog profesora ceka dok prvi ne zavrsi transakciju, pa
// tek onda radi proveru konflikta i vidi vec upisan termin. Lock se otpusta sam na
// commit/rollback, pa ne moze da "ostane visiti" ako proces padne.
public class ProfessorScheduleLock : IProfessorScheduleLock
{
    private readonly GroupDbContext _db;

    public ProfessorScheduleLock(GroupDbContext db)
    {
        _db = db;
    }

    public async Task<IScheduleLockScope> AcquireAsync(
        IEnumerable<Guid> professorIds, CancellationToken cancellationToken = default)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Uvek isti redosled zakljucavanja, da dva zahteva sa istim profesorima ne
            // zakljucaju jedan drugog (deadlock).
            foreach (var professorId in professorIds.Distinct().Order())
            {
                var key = $"professor-schedule:{professorId}";
                await _db.Database.ExecuteSqlAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            }

            return new Scope(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class Scope : IScheduleLockScope
    {
        private readonly IDbContextTransaction _transaction;

        public Scope(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            _transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
