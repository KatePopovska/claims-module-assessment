using ClaimsModule.Domain.Common;

namespace ClaimsModule.Persistence.Idempotency;

public class IdempotencyRecord : BaseAuditableEntity
{
    public string Key { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public int? StatusCode { get; set; }
    public string? ContentType { get; set; }
    public string? Location { get; set; }
    public string? ResponseBody { get; set; }
}
