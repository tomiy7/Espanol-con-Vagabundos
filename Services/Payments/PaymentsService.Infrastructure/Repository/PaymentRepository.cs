using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaymentsService.Application.Interfaces;
using PaymentsService.Domain.Entities;
using PaymentsService.Domain.Enums;
using PaymentsService.Infrastructure.Data;

namespace PaymentsService.Infrastructure.Repository;

public class PaymentRepository : RepositoryBase<Payment>, IPaymentRepository
{
    private readonly PaymentDbContext _dbContext;
    
    public PaymentRepository(PaymentDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task<Payment?> GetByIdAsync(Guid id)
    {
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.Id == id);

        if (payment != null && payment.HasExpired(DateTime.UtcNow))
        {
            payment.MarkAsExpired();
            await _dbContext.SaveChangesAsync();
        }
        
        return payment;
    }

    public async Task<List<Payment>> GetPendingAsync() =>
        await _dbContext.Payments.Where(p => p.Status == PaymentStatus.Pending).ToListAsync();

    public async Task<List<Payment>> GetByUserIdAsync(Guid userId) =>
        await _dbContext.Payments.Where(p => p.UserId == userId).ToListAsync();

    public async Task<List<Payment>> GetByCourseIdAsync(Guid courseId) =>
        await _dbContext.Payments.Where(p => p.CourseId == courseId).ToListAsync();

    public async Task<Payment?> GetActivePendingForUserAndCourseAsync(Guid userId, Guid courseId) =>
        await _dbContext.Payments.FirstOrDefaultAsync(p =>
            p.UserId == userId &&
            p.CourseId == courseId &&
            p.Status == PaymentStatus.Pending);
}