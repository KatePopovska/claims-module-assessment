using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Reference;

public class Policy : BaseAuditableEntity, ISoftDelete
{
    public string PolicyNumber { get; set; } = null!;
    public string ClientName { get; set; } = null!;
    public DateTimeOffset EffectiveDate { get; set; }
    public DateTimeOffset ExpirationDate { get; set; }
    public PolicyStatus Status { get; set; }
    public string CoverageTypes { get; set; } = null!;

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
