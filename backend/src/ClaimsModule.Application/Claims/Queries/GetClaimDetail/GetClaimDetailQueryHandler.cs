using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries.GetClaimDetail;

public class GetClaimDetailQueryHandler(IApplicationDbContext context) : IRequestHandler<GetClaimDetailQuery, ClaimDetailDto>
{
    public async Task<ClaimDetailDto> Handle(GetClaimDetailQuery request, CancellationToken cancellationToken)
    {
        var claim = await context.Claims
            .AsNoTracking()
            .Where(c => c.Id == request.ClaimId)
            .Select(ClaimDetailProjections.Claim)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        return ToDto(claim);
    }

    private static ClaimDetailDto ToDto(ClaimDetailProjection claim) => new(
        claim.Id,
        claim.ClaimNumber,
        claim.Status,
        ClaimStatusTransitions.GetValidNextStatuses(claim.Status),
        claim.Severity,
        claim.PolicyId,
        claim.PolicyNumber,
        claim.ClientName,
        claim.ReportedDate,
        claim.AssignedHandlerId,
        claim.ClosedAt,
        claim.ClosureReason,
        claim.Notes,
        claim.LossEvent,
        claim.Parties,
        claim.RiskObjects,
        new ReserveSummaryDto(claim.ReserveComponents.Sum(rc => rc.CurrentBalance), claim.ReserveComponents),
        claim.Documents,
        claim.RecentAuditEntries);
}
