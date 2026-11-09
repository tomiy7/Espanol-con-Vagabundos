using GroupsService.Application.Interfaces;
using GroupsService.Domain.Entities;
using GroupsService.Domain.Enums;
using GroupsService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GroupsService.Infrastructure.Repository;

public class GroupRepository : RepositoryBase<Group>, IGroupRepository
{
    private readonly GroupDbContext _db;

    public GroupRepository(GroupDbContext dbContext) : base(dbContext)
    {
        _db = dbContext;
    }
    
    // OwnsMany kolekcije zahtevaju eksplicitan Include da se ucitaju.
    public override async Task<Group?> GetByIdAsync(Guid id) =>
        await _db.Groups
            .Include(g => g.Members)
            .Include(g => g.Slots)
            .FirstOrDefaultAsync(g => g.Id == id);

    public override async Task<List<Group>> GetAllAsync() =>
        await _db.Groups.Include(g => g.Members).Include(g => g.Slots).ToListAsync();
    
    public async Task<List<Group>> GetListAsync(
        Guid? courseId, GroupStatus? status, GroupType? type, Guid? professorId)
    {
        var query = _db.Groups.Include(g => g.Members).Include(g => g.Slots).AsQueryable();

        if (courseId.HasValue) query = query.Where(g => g.CourseId == courseId.Value);
        if (status.HasValue) query = query.Where(g => g.Status == status.Value);
        if (type.HasValue) query = query.Where(g => g.Type == type.Value);
        if (professorId.HasValue) query = query.Where(g => g.Slots.Any(s => s.ProfessorId == professorId.Value));

        var groups = await query.ToListAsync();
        return groups.OrderByDescending(g => g.CreatedAt).ToList();
    }

    // Samo GroupType.Group - to su jedine grupe u koje se pridruzuje "otvoreno"
    // (frontend lista za pregled/pridruzivanje). Pair se formira preko mejl poziva
    // (invite-partner), a Individual je privatna 1-clana grupa (samo koordinacija
    // potvrde termina) - ni jedna od ove dve ne treba da se vidi kao "dostupna grupa".
    public async Task<List<Group>> GetAvailableForCourseAsync(Guid courseId)
    {
        var groups = await _db.Groups
            .Include(g => g.Members)
            .Include(g => g.Slots)
            .Where(g => g.CourseId == courseId && g.Type == GroupType.Group)
            .ToListAsync();

        return groups.OrderBy(g => g.CreatedAt).ToList();
    }

    public async Task<bool> HasConflictingSlotAsync(
        Guid professorId, DayOfWeek dayOfWeek, TimeOnly time, Guid excludeGroupId)
    {
        // Direktan upit nad slot kolekcijom (owned tabela group_slots) - ne ucitava
        // cele Group agregate (Members i sl.) samo da bi se provirila jedna kolona.
        return await _db.Groups
            .Where(g => g.Id != excludeGroupId && g.Status != GroupStatus.Completed)
            .SelectMany(g => g.Slots)
            .AnyAsync(s => s.ProfessorId == professorId && s.DayOfWeek == dayOfWeek && s.Time == time);
    }
}