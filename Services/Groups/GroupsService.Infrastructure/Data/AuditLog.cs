using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GroupsService.Application.Interfaces;

namespace GroupsService.Infrastructure.Data;

public class AuditLog : IAuditLog
{
    private readonly GroupDbContext _db;
    private readonly ILogger<AuditLog> _logger;

    public AuditLog(GroupDbContext db, ILogger<AuditLog> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Best-effort: neuspeh upisa se samo loguje, ne obara vec uradjenu operaciju.
    // Napomena: DbContext je isti kao u repozitorijumima (scoped), pa SaveChangesAsync
    // ovde snima i sve sto je jos nesnimljeno. Zato se poziva tek posle handlera,
    // kad je poslovna promena vec snimljena.
    public async Task RecordAsync(Guid actorUserId, string action, string entityType, Guid entityId, string? details = null)
    {
        try
        {
            _db.AuditEntries.Add(new AuditEntry
            {
                Id = Guid.NewGuid(),
                ActorUserId = actorUserId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details is { Length: > 500 } ? details[..500] : details,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Writing to the audit log failed ({Action} {EntityType} {EntityId}).", action, entityType, entityId);
        }
    }

    public async Task<List<AuditEntryDto>> GetAsync(Guid? entityId, Guid? actorUserId, int take)
    {
        var query = _db.AuditEntries.AsNoTracking().AsQueryable();
        if (entityId.HasValue) query = query.Where(a => a.EntityId == entityId.Value);
        if (actorUserId.HasValue) query = query.Where(a => a.ActorUserId == actorUserId.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(a => new AuditEntryDto(
                a.Id, a.ActorUserId, a.Action, a.EntityType, a.EntityId, a.Details, a.CreatedAt))
            .ToListAsync();
    }
}
