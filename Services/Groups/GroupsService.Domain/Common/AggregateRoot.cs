namespace GroupsService.Domain;

public class AggregateRoot
{
    private readonly List<object> _domainEvents = new();
    
    public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();
    
    protected void AddDomainEvent(object domainEvent) => _domainEvents.Add(domainEvent);
    
    public void ClearDomainEvents() => _domainEvents.Clear();
}