using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Claims;

public static class ClaimStatusTransitions
{
    private static readonly string[] AnyClaimsRole = [UserRoles.Handler, UserRoles.Supervisor, UserRoles.Manager];
    private static readonly string[] SupervisorOrManager = [UserRoles.Supervisor, UserRoles.Manager];

    private static readonly Dictionary<(ClaimStatus From, ClaimStatus To), string[]> PermittedRoles = new()
    {
        [(ClaimStatus.Draft, ClaimStatus.Open)] = AnyClaimsRole,
        [(ClaimStatus.Open, ClaimStatus.UnderInvestigation)] = AnyClaimsRole,
        [(ClaimStatus.Open, ClaimStatus.PendingPayment)] = AnyClaimsRole,
        [(ClaimStatus.Open, ClaimStatus.Closed)] = AnyClaimsRole,
        [(ClaimStatus.Open, ClaimStatus.Withdrawn)] = AnyClaimsRole,
        [(ClaimStatus.UnderInvestigation, ClaimStatus.Open)] = AnyClaimsRole,
        [(ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment)] = AnyClaimsRole,
        [(ClaimStatus.UnderInvestigation, ClaimStatus.Closed)] = AnyClaimsRole,
        [(ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn)] = AnyClaimsRole,
        [(ClaimStatus.PendingPayment, ClaimStatus.Closed)] = AnyClaimsRole,
        [(ClaimStatus.Closed, ClaimStatus.Reopened)] = SupervisorOrManager,
        [(ClaimStatus.Reopened, ClaimStatus.Open)] = AnyClaimsRole
    };

    public static bool IsValid(ClaimStatus from, ClaimStatus to) => PermittedRoles.ContainsKey((from, to));

    public static IReadOnlyCollection<ClaimStatus> GetValidNextStatuses(ClaimStatus from) =>
        PermittedRoles.Keys.Where(t => t.From == from).Select(t => t.To).ToList();

    public static bool IsPermittedFor(ClaimStatus from, ClaimStatus to, string? role) =>
        role is not null
        && PermittedRoles.TryGetValue((from, to), out var roles)
        && roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
