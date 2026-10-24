namespace GroupsService.Application.Interfaces;

public record UserContact(Guid UserId, string Email, string FullName);

// Groups servis ne cuva email/ime korisnika - to zivi u Auth servisu. Ovaj interfejs
// razdvaja "treba mi kontakt podatak korisnika" od KAKO se do njega dolazi (HTTP poziv
// ka Auth GET /admin/users/{id}, implementacija u Infrastructure/Clients).
public interface IUserDirectory
{
    Task<UserContact?> GetUserContactAsync(Guid userId, CancellationToken ct = default);
}