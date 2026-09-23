using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common.Services;

public class AuditLogService(IApplicationDbContext context, ICorrelationIdProvider correlationIdProvider) : IAuditLogService
{
    // Stages the entry only — the caller's IUnitOfWork.SaveChangesAsync commits it together
    // with the rest of the command's changes, so an audit entry never exists without the
    // business change it describes (or vice versa).
    public void Log(
        Guid claimId,
        AuditEventType eventType,
        string description,
        string? oldValue = null,
        string? newValue = null,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        context.ClaimAuditLog.Add(new ClaimAuditLog
        {
            ClaimId = claimId,
            EventType = eventType,
            Description = description,
            OldValue = oldValue,
            NewValue = newValue,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            CorrelationId = correlationIdProvider.CorrelationId
        });
    }
}
