using GroupsService.Domain.Entities;
using GroupsService.Domain.Interfaces;

namespace GroupsService.Application.Interfaces;

public interface IEnrollmentRepository : IAsyncRepository<Enrollment>
{
    Task<List<Enrollment>> GetByUserIdAsync(Guid userId);
    Task<List<Enrollment>> GetByGroupIdAsync(Guid groupId);
    Task<Enrollment?> GetActiveOrSearchingAsync(Guid userId, Guid courseId);
}