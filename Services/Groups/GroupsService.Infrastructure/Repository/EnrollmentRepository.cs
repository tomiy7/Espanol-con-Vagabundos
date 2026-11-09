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
    
    // OwnsMany kolekcije (napredak) se eksplicitno Include-uju, isto kao u GroupRepository.
    public override async Task<Enrollment?> GetByIdAsync(Guid id) =>
        await _db.Enrollments
            .Include(e => e.CompletedLessons)
            .FirstOrDefaultAsync(e => e.Id == id);

    public async Task<List<Enrollment>> GetByUserIdAsync(Guid userId) =>
        await _db.Enrollments
            .Include(e => e.CompletedLessons)
            .Where(e => e.UserId == userId)
            .ToListAsync();

    public async Task<List<Enrollment>> GetByGroupIdAsync(Guid groupId) =>
        await _db.Enrollments
            .Include(e => e.CompletedLessons)
            .Where(e => e.GroupId == groupId)
            .ToListAsync();

    public async Task<Enrollment?> GetNonCancelledAsync(Guid userId, Guid courseId) =>
        await _db.Enrollments.FirstOrDefaultAsync(e =>
            e.UserId == userId && e.CourseId == courseId &&
            e.Status != EnrollmentStatus.Cancelled);
}