namespace ClaimsModule.Domain.Common;

public abstract class BaseEntity
{
    // Not client-generated: EF Core defers to the database's sequential-GUID default.
    public Guid Id { get; set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
