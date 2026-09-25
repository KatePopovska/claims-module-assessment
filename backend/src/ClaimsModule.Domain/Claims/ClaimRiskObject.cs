using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Claims;

public class ClaimRiskObject : BaseAuditableEntity, ISoftDelete, IClaimChild
{
    public Guid ClaimId { get; set; }
    public Claim Claim { get; set; } = null!;

    public AssetType AssetType { get; set; }
    public string AssetDescription { get; set; } = null!;
    public string? DamageDescription { get; set; }
    public bool IsPrimary { get; set; }
    public string? AssetReference { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
