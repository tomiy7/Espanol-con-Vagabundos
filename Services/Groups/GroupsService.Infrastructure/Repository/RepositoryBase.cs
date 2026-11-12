using GroupsService.Domain;
using GroupsService.Domain.Exceptions;
using GroupsService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

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
            throw new GroupsDomainException(
                "The data was changed in the meantime (e.g. the group just filled up). Please try again.");
        }
    }
}