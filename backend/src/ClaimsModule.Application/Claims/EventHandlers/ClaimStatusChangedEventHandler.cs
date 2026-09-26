using ClaimsModule.Application.Common.Events;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.EventHandlers;

public class ClaimStatusChangedEventHandler(IAuditLogService auditLogService) : INotificationHandler<DomainEventNotification<ClaimStatusChangedEvent>>
{
    public Task Handle(DomainEventNotification<ClaimStatusChangedEvent> notification, CancellationToken cancellationToken)
    {
        var change = notification.DomainEvent;

        var description = change.IsAutomatic
            ? $"Status changed automatically from {change.From} to {change.To}."
            : $"Status changed from {change.From} to {change.To}.";

        if (change.AcknowledgedWarnings.Count > 0)
        {
            description += $" Acknowledged warnings: {string.Join(" ", change.AcknowledgedWarnings)}";
        }

        if (!string.IsNullOrWhiteSpace(change.Reason))
        {
            description += $" Reason: {change.Reason}";
        }

        auditLogService.Log(change.Claim, AuditEventType.STATUS_CHANGED, description, oldValue: change.From.ToString(), newValue: change.To.ToString());

        switch (change.To)
        {
            case ClaimStatus.Closed:
                auditLogService.Log(change.Claim, AuditEventType.CLAIM_CLOSED, "Claim closed.", newValue: change.Reason);
                break;

            case ClaimStatus.Reopened:
                auditLogService.Log(change.Claim, AuditEventType.CLAIM_REOPENED, "Claim reopened.", newValue: change.Reason);
                break;
        }

        return Task.CompletedTask;
    }
}
