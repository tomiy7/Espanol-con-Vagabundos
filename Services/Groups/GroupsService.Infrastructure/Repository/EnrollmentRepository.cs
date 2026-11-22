using GroupsService.Application.Interfaces;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GroupsService.Infrastructure.Repository;

public class EnrollmentRepository : RepositoryBase<Enrollment>, IEnrollmentRepository
{
    private readonly GroupDbContext _db;

    public EnrollmentRepository(GroupDbContext dbContext) : base(dbContext)
    {
        _db = dbContext;
    }
    
    public async Task<List<Enrollment>> GetByUserIdAsync(Guid userId) =>
        await _db.Enrollments
            .Where(e => e.UserId == userId)
            .ToListAsync();

    public async Task<List<Enrollment>> GetByGroupIdAsync(Guid groupId) =>
        await _db.Enrollments
            .Where(e => e.GroupId == groupId)
            .ToListAsync();

    public async Task<Enrollment?> GetNonCancelledAsync(Guid userId, Guid courseId) =>
        await _db.Enrollments.FirstOrDefaultAsync(e =>
            e.UserId == userId && e.CourseId == courseId &&
            e.Status != EnrollmentStatus.Cancelled);
}