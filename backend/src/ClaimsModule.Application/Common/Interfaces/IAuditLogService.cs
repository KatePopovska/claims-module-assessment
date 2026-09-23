using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common.Interfaces;

public interface IAuditLogService
{
    void Log(
        Guid claimId,
        AuditEventType eventType,
        string description,
        string? oldValue = null,
        string? newValue = null,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null);
}
