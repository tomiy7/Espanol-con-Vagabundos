using PaymentsService.Domain.Entities;
using PaymentsService.Domain.Interfaces;

namespace PaymentsService.Application.Interfaces;

public interface IPaymentRepository : IAsyncRepository<Payment>
{
    Task<List<Payment>> GetPendingAsync();
    Task<List<Payment>> GetByUserIdAsync(Guid userId);
    Task<Payment?> GetActivePendingForUserAndCourseAsync(Guid userId, Guid courseId);
}