using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Audit;

public class ClaimAuditLog : BaseAuditableEntity
{
    public Guid ClaimId { get; set; }
    public Claim Claim { get; set; } = null!;

    public AuditEventType EventType { get; set; }
    public string Description { get; set; } = null!;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }

    public string? CorrelationId { get; set; }
}
