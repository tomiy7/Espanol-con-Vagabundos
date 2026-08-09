using AuthService.API.Entities;

namespace AuthService.API.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByIdAsync(Guid id);
    Task<User?> GetByUsenameAsync(string username);
    Task<User?> GetByIdentifierAsync(string identifier);
    Task<List<User>>  GetAllUsersAsync();
    Task<bool> EmailExistsAsync(string email);
    Task<bool> UsernameExistsAsync(string username);
    Task AddUserAsync(User user);
    Task SaveChangesAsync();
}