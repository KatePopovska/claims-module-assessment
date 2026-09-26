using ClaimsModule.Application.Common.Events;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.EventHandlers;

public class ClaimCreatedEventHandler(IAuditLogService auditLogService) : INotificationHandler<DomainEventNotification<ClaimCreatedEvent>>
{
    public Task Handle(DomainEventNotification<ClaimCreatedEvent> notification, CancellationToken cancellationToken)
    {
        var claim = notification.DomainEvent.Claim;

        auditLogService.Log(claim, AuditEventType.CLAIM_CREATED, $"Claim {claim.ClaimNumber} created via FNOL intake.");

        return Task.CompletedTask;
    }
}
