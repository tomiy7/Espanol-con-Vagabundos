using GroupsService.Domain.Entities;

namespace GroupsService.Application.Interfaces;

public interface IEnrollmentRepository
{
    Task<List<Enrollment>> GetByUserIdAsync(Guid userId);
    Task<List<Enrollment>> GetByGroupIdAsync(Guid groupId);
    Task<Enrollment?> GetActiveOrSearchingAsync(Guid userId, Guid courseId);
}