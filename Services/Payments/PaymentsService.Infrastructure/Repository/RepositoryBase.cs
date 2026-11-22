using Microsoft.EntityFrameworkCore;
using PaymentsService.Domain;
using PaymentsService.Domain.Interfaces;

namespace PaymentsService.Infrastructure.Repository;

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

    public async Task<bool> SaveChangesAsync() =>
        await _dbContext.SaveChangesAsync() > 0;
}