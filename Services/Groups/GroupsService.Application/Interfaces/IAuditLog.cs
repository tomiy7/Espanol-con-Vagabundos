namespace GroupsService.Application.Interfaces;

public record AuditEntryDto(
    Guid Id, Guid ActorUserId, string Action, string EntityType, Guid EntityId, string? Details, DateTime CreatedAt);

// Trag ko je sta uradio (profesori i admini rade zajedno, pa ovo omogucava adminu da
// vidi i ispravi gresku). Zapis je best-effort: neuspeh upisa u audit ne sme da obori
// vec uradjenu operaciju, vec se samo loguje.
public interface IAuditLog
{
    Task RecordAsync(Guid actorUserId, string action, string entityType, Guid entityId, string? details = null);

    Task<List<AuditEntryDto>> GetAsync(Guid? entityId, Guid? actorUserId, int take);
}