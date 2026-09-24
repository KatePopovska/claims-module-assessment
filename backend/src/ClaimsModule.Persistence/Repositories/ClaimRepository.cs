using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

public class ClaimRepository(IApplicationDbContext context) : IClaimRepository
{
    public void Add(Claim claim) => context.Claims.Add(claim);

    public Task<Claim?> GetByIdAsync(Guid claimId, CancellationToken cancellationToken = default) =>
        context.Claims
            .Include(c => c.Policy)
            .Include(c => c.LossEvent)
            .Include(c => c.Parties)
            .Include(c => c.ReserveComponents).ThenInclude(rc => rc.History)
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);
}
