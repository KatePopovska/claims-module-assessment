using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Common.Events;

public record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification where TEvent : IDomainEvent;

public static class DomainEventNotification
{
    public static INotification For(IDomainEvent domainEvent) =>
        (INotification)Activator.CreateInstance(typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType()), domainEvent)!;
}
