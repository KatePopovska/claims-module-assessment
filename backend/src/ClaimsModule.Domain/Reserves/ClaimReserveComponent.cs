using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Reserves;

public class ClaimReserveComponent : BaseAuditableEntity, ISoftDelete, IHasConcurrencyToken
{
    public Guid ClaimId { get; set; }
    public Claim Claim { get; set; } = null!;

    public ReserveComponentType Component { get; set; }
    public decimal CurrentAmount { get; set; }
    public ReserveComponentStatus Status { get; set; } = ReserveComponentStatus.Active;
    public string? Notes { get; set; }

    public byte[] RowVer { get; set; } = [];

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<ReserveHistory> History { get; set; } = [];
}
