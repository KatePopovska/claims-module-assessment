using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Claims;

public class ClaimParty : BaseAuditableEntity
{
    public Guid ClaimId { get; set; }
    public Claim Claim { get; set; } = null!;

    public PartyRole PartyRole { get; set; }
    public PartyType PartyType { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public string GetDisplayName() => PartyType == PartyType.Company
        ? CompanyName ?? string.Empty
        : $"{FirstName} {LastName}".Trim();
}
