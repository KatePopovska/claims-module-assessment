using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reference;

namespace ClaimsModule.Domain.Claims;

public class LossEvent : BaseAuditableEntity, ISoftDelete, IClaimChild
{
    public Guid ClaimId { get; set; }
    public Claim Claim { get; set; } = null!;

    public DateTimeOffset LossDate { get; set; }
    public string LossDescription { get; set; } = null!;
    public string? LossLocation { get; set; }

    public string CauseOfLossCode { get; set; } = null!;
    public CauseOfLossCode CauseOfLossCodeReference { get; set; } = null!;

    public decimal? EstimatedLossAmount { get; set; }
    public DateTimeOffset ReportDate { get; set; }
    public string? PoliceReportNumber { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
