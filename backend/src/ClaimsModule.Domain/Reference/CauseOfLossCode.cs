using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Reference;

public class CauseOfLossCode : BaseAuditableEntity, ISoftDelete
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public PerilCategory PerilCategory { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
