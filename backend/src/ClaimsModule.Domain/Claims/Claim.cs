using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.Reference;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Domain.Claims;

public class Claim : BaseAuditableEntity, ISoftDelete, IHasConcurrencyToken
{
    public string ClaimNumber { get; set; } = null!;

    public Guid? PolicyId { get; set; }
    public Policy? Policy { get; set; }

    public string? PolicyNumber { get; set; }
    public string? ClientName { get; set; }

    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    public Severity? Severity { get; set; }

    public DateTimeOffset ReportedDate { get; set; }
    public Guid? AssignedHandlerId { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosureReason { get; set; }
    public string? Notes { get; set; }

    public bool ReserveLimitOverride { get; set; }
    public Guid? ReserveLimitOverrideByUserId { get; set; }
    public DateTimeOffset? ReserveLimitOverrideAt { get; set; }
    public string? ReserveLimitOverrideReason { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public byte[] RowVer { get; set; } = [];

    public LossEvent? LossEvent { get; set; }
    public ICollection<ClaimParty> Parties { get; set; } = [];
    public ICollection<ClaimRiskObject> RiskObjects { get; set; } = [];
    public ICollection<ClaimReserveComponent> ReserveComponents { get; set; } = [];
    public ICollection<ClaimDocument> Documents { get; set; } = [];
    public ICollection<ClaimAuditLog> AuditLogEntries { get; set; } = [];
}
