using GroupsService.Domain;
using GroupsService.Domain.Exceptions;
using GroupsService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GroupsService.Infrastructure.Repository;

public abstract class RepositoryBase<T> : IAsyncRepository<T> where T : AggregateRoot
{
    protected readonly DbContext _dbContext;

    public RepositoryBase(DbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public virtual async Task<T?> GetByIdAsync(Guid id) =>
        await _dbContext.Set<T>().FirstOrDefaultAsync(e => e.Id == id);
    
    public virtual async Task<List<T>> GetAllAsync() =>
        await _dbContext.Set<T>().ToListAsync();

    public async Task AddAsync(T entity) =>
        await _dbContext.Set<T>().AddAsync(entity);

    public void Update(T entity) =>
        _dbContext.Set<T>().Update(entity);

    public void Delete(T entity) =>
        _dbContext.Set<T>().Remove(entity);

    public async Task<bool> SaveChangesAsync()
    {
        try
        {
            return await _dbContext.SaveChangesAsync() > 0;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Neko je izmenio isti agregat (npr. zadnje mesto u grupi) izmedju citanja
            // i upisa. API ovo mapira kao domensku gresku (konflikt).
            throw new ConcurrencyConflictException(
                "The data was changed in the meantime (e.g. the group just filled up). Please try again.");
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            // Baza je odbila duplikat koji provera u handleru nije uhvatila jer su dva
            // zahteva stigla istovremeno (npr. dupli klik). Klijent dobija poslovnu gresku.
            throw new ConcurrencyConflictException(
                pg.ConstraintName == EnrollmentUniqueIndex
                    ? "You already have an active or pending enrollment for this course."
                    : "The data conflicts with an existing record. Please refresh and try again.");
        }
    }

    // Naziv jedinstvenog indeksa (korisnik, kurs) za neotkazane upise - vidi EnrollmentConfiguration.
    private const string EnrollmentUniqueIndex = "IX_enrollments_user_id_course_id";
}