using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common.Services;

public class AuditLogService(
    IApplicationDbContext context,
    ICorrelationIdProvider correlationIdProvider,
    TimeProvider timeProvider) : IAuditLogService
{
    private DateTimeOffset _lastCreatedAt;
    public void Log(
        Claim claim,
        AuditEventType eventType,
        string description,
        string? oldValue = null,
        string? newValue = null,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        context.ClaimAuditLog.Add(new ClaimAuditLog
        {
            Claim = claim,
            EventType = eventType,
            Description = description,
            OldValue = oldValue,
            NewValue = newValue,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            CorrelationId = correlationIdProvider.CorrelationId,
            CreatedAt = NextCreatedAt()
        });
    }

    private DateTimeOffset NextCreatedAt()
    {
        var now = timeProvider.GetUtcNow();
        _lastCreatedAt = now > _lastCreatedAt ? now : _lastCreatedAt.AddTicks(1);
        return _lastCreatedAt;
    }
}
