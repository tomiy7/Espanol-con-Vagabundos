using GroupsService.Application.Interfaces;
using GroupsService.Domain;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Domain.Interfaces;

namespace GroupsService.Application.Tests;

// Jednostavni in-memory repozitorijumi. Agregati su isti objekti izmedju poziva
// (kao tracked entiteti u EF-u), pa promene iz handlera ostaju vidljive.
public abstract class FakeRepo<T> : IAsyncRepository<T> where T : AggregateRoot
{
    public List<T> Items { get; } = new();

    public Task<T?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<List<T>> GetAllAsync() => Task.FromResult(Items.ToList());

    public Task AddAsync(T entity)
    {
        if (!Items.Contains(entity)) Items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity) { }
    public void Delete(T entity) => Items.Remove(entity);
    public Task<bool> SaveChangesAsync() => Task.FromResult(true);
}

public class FakeEnrollmentRepository : FakeRepo<Enrollment>, IEnrollmentRepository
{
    public Task<List<Enrollment>> GetByUserIdAsync(Guid userId) =>
        Task.FromResult(Items.Where(e => e.UserId == userId).ToList());

    public Task<List<Enrollment>> GetByGroupIdAsync(Guid groupId) =>
        Task.FromResult(Items.Where(e => e.GroupId == groupId).ToList());

    public Task<Enrollment?> GetNonCancelledAsync(Guid userId, Guid courseId) =>
        Task.FromResult(Items.FirstOrDefault(e =>
            e.UserId == userId && e.CourseId == courseId && e.Status != EnrollmentStatus.Cancelled));
}

public class FakeGroupRepository : FakeRepo<Group>, IGroupRepository
{
    public Task<List<Group>> GetListAsync(Guid? courseId, GroupStatus? status, GroupType? type, Guid? professorId) =>
        Task.FromResult(Items.ToList());

    public Task<List<Group>> GetAvailableForCourseAsync(Guid courseId) => Task.FromResult(Items.ToList());

    public Task<bool> HasConflictingSlotAsync(Guid professorId, DayOfWeek dayOfWeek, TimeOnly time, Guid excludeGroupId) =>
        Task.FromResult(false);
}

public class FakeInvitationRepository : FakeRepo<PartnerInvitation>, IPartnerInvitationRepository
{
    public Task<PartnerInvitation?> GetByTokenAsync(string token) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Token == token));

    public Task<List<PartnerInvitation>> GetByInviterAndInviteeAsync(Guid inviterEnrollmentId, Guid inviteeUserId) =>
        Task.FromResult(Items.Where(i => i.InviterEnrollmentId == inviterEnrollmentId
                                         && i.InviteeUserId == inviteeUserId).ToList());
}
