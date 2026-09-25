using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Reserves;

public static class ReserveAuthority
{
    public const decimal AutoApprovalLimit = 10_000m;
    public const decimal SupervisorApprovalLimit = 100_000m;
    public const decimal ClaimReserveLimit = 10_000_000m;

    public static bool IsWithinAutoApprovalLimit(decimal amount) => Math.Abs(amount) <= AutoApprovalLimit;

    public static bool CanApprove(string? role, decimal amount) =>
        Math.Abs(amount) <= SupervisorApprovalLimit
            ? IsRole(role, UserRoles.Supervisor) || IsRole(role, UserRoles.Manager)
            : IsRole(role, UserRoles.Manager);

    public static bool IsManager(string? role) => IsRole(role, UserRoles.Manager);

    private static bool IsRole(string? role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
}
