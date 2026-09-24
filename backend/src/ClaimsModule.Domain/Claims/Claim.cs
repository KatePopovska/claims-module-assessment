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

    public bool IsLastActiveClaimant(ClaimParty party) =>
        party is { IsActive: true, PartyRole: PartyRole.Claimant }
        && Parties.Count(p => p.IsActive && p.PartyRole == PartyRole.Claimant) == 1;

    public IReadOnlyList<StatusChange> ChangeStatus(ClaimStatus targetStatus, string? reason, DateTimeOffset now)
    {
        if (!ClaimStatusTransitions.IsValid(Status, targetStatus))
        {
            throw new InvalidOperationException($"Transition from {Status} to {targetStatus} is not permitted.");
        }

        var changes = new List<StatusChange> { new(Status, targetStatus, IsAutomatic: false) };
        Status = targetStatus;

        switch (targetStatus)
        {
            case ClaimStatus.Closed:
                ClosedAt = now;
                ClosureReason = reason;
                break;

            case ClaimStatus.Reopened:
                ClosedAt = null;
                ClosureReason = null;
                changes.Add(new StatusChange(ClaimStatus.Reopened, ClaimStatus.Open, IsAutomatic: true));
                Status = ClaimStatus.Open;
                break;
        }

        return changes;
    }
}
