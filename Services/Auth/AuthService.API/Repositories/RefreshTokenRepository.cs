using AuthService.API.Data;
using AuthService.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.API.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _dbContext;
    
    public RefreshTokenRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<RefreshToken?> GetByTokenAsync(string token) =>
        await _dbContext.RefreshTokens
            .Include(u => u.User)
            .FirstOrDefaultAsync(x => x.Token == token);

    public async Task AddAsync(RefreshToken refreshToken) =>
        await _dbContext.RefreshTokens.AddAsync(refreshToken);

    public async Task SaveChangesAsync() =>
        await _dbContext.SaveChangesAsync();
}