using ClaimsModule.Application.Claims.Queries.GetClaimDetail;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Reserves.Queries.GetClaimReserves;

public class GetClaimReservesQueryHandler(IApplicationDbContext context) : IRequestHandler<GetClaimReservesQuery, ClaimReservesDto>
{
    public async Task<ClaimReservesDto> Handle(GetClaimReservesQuery request, CancellationToken cancellationToken)
    {
        var claim = await context.Claims
            .AsNoTracking()
            .Where(c => c.Id == request.ClaimId)
            .Select(c => new
            {
                c.ReserveLimitOverride,
                Components = c.ReserveComponents.AsQueryable()
                    .OrderBy(rc => rc.Component)
                    .Select(ClaimDetailProjections.ReserveComponent)
                    .ToList(),
                Transactions = c.ReserveComponents.AsQueryable()
                    .SelectMany(rc => rc.History)
                    .OrderByDescending(h => h.CreatedAt)
                    .Select(ReserveProjections.Transaction)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        return new ClaimReservesDto(claim.Components.Sum(rc => rc.CurrentBalance), claim.ReserveLimitOverride, claim.Components, claim.Transactions);
    }
}
