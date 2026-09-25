using System.Linq.Expressions;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Claims;

public static class ClaimSla
{
    public const string BreachDescription = "Claim has not been updated in 48 hours";

    public static readonly TimeSpan BreachThreshold = TimeSpan.FromHours(48);
    public static readonly TimeSpan BreachRepeatInterval = TimeSpan.FromHours(24);

    private static readonly ClaimStatus[] MonitoredStatuses = [ClaimStatus.Draft, ClaimStatus.Open];

    public static Expression<Func<Claim, bool>> IsBreachDue(DateTimeOffset now)
    {
        var staleBefore = now - BreachThreshold;
        var lastBreachBefore = now - BreachRepeatInterval;

        return c => MonitoredStatuses.Contains(c.Status)
            && (c.UpdatedAt ?? c.CreatedAt) < staleBefore
            && !c.AuditLogEntries.Any(a => a.EventType == AuditEventType.SLA_BREACH_DETECTED && a.CreatedAt > lastBreachBefore);
    }
}
