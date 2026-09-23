using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Persistence.Repositories;

public class ClaimRepository(IApplicationDbContext context) : IClaimRepository
{
    public void Add(Claim claim) => context.Claims.Add(claim);
}
