using AuthService.API.Data;
using AuthService.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.API.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<User?> GetUserByEmailAsync(string email) =>
        await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == email);
    

    public async Task<User?> GetUserByIdAsync(Guid id) =>
        await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
    

    public async Task<User?> GetByUsenameAsync(string username) =>
        await _dbContext.Users.FirstOrDefaultAsync(x => x.Username == username);
    
    public async Task<User?> GetByIdentifierAsync(string identifier) =>
        await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == identifier || u.Username == identifier);

    public async Task<List<User>> GetAllUsersAsync() =>
        await _dbContext.Users.ToListAsync();

    public async Task<bool> EmailExistsAsync(string email) =>
        await _dbContext.Users.AnyAsync(x => x.Email == email);

    public async Task<bool> UsernameExistsAsync(string username) =>
        await _dbContext.Users.AnyAsync(x => x.Username == username);

    public async Task AddUserAsync(User user) =>
        await _dbContext.Users.AddAsync(user);

    public async Task SaveChangesAsync() =>
        await _dbContext.SaveChangesAsync();
}