using ClaimsModule.Application.Common.Interfaces;

namespace ClaimsModule.Persistence;

public class UnitOfWork(IApplicationDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
