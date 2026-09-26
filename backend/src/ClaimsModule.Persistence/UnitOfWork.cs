using ClaimsModule.Application.Common.Events;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClaimsModule.Persistence;

public class UnitOfWork(ClaimsDbContext context, IPublisher publisher) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(cancellationToken);
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => new UnitOfWorkTransaction(await context.Database.BeginTransactionAsync(cancellationToken));

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var entities = context.ChangeTracker.Entries<BaseEntity>()
                .Select(e => e.Entity)
                .Where(e => e.DomainEvents.Count > 0)
                .ToList();

            if (entities.Count == 0)
            {
                return;
            }

            var domainEvents = entities.SelectMany(e => e.DomainEvents).ToList();
            entities.ForEach(e => e.ClearDomainEvents());

            foreach (var domainEvent in domainEvents)
            {
                await publisher.Publish(DomainEventNotification.For(domainEvent), cancellationToken);
            }
        }
    }

    private sealed class UnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
