namespace ClaimsModule.Domain.Common;

public abstract class BaseAuditableEntity : BaseEntity
{
    public Guid OrganisationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UserCreated { get; set; }
    public Guid? UserModified { get; set; }
}
