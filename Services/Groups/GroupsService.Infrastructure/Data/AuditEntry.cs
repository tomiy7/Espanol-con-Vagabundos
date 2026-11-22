namespace GroupsService.Infrastructure.Data;

// Zapis o osetljivoj akciji osoblja (ko, sta, nad cim, kada). Nije domenski agregat,
// pa zivi u Infrastructure sloju; Application vidi samo IAuditLog i AuditEntryDto.
public class AuditEntry
{
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}