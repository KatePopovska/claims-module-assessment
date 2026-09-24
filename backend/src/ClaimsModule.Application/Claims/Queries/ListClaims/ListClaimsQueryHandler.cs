using ClaimsModule.Application.Common.Extensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries.ListClaims;

public class ListClaimsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListClaimsQuery, PagedResult<ClaimSummaryDto>>
{
    public async Task<PagedResult<ClaimSummaryDto>> Handle(ListClaimsQuery request, CancellationToken cancellationToken)
    {
        var claims = ApplyFilters(context.Claims.AsNoTracking(), request);

        var totalCount = await claims.CountAsync(cancellationToken);

        var items = await claims
            .OrderByDescending(c => c.ReportedDate)
            .ThenByDescending(c => c.ClaimNumber)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ClaimSummaryDto(
                c.Id,
                c.ClaimNumber,
                c.PolicyNumber,
                c.ClientName,
                c.LossEvent != null ? c.LossEvent.LossDate : null,
                c.LossEvent != null ? c.LossEvent.CauseOfLossCode : null,
                c.LossEvent != null ? c.LossEvent.CauseOfLossCodeReference.Name : null,
                c.Status,
                c.AssignedHandlerId,
                c.ReserveComponents
                    .SelectMany(rc => rc.History)
                    .Where(h => h.ApprovalStatus == ReserveApprovalStatus.Approved || h.ApprovalStatus == ReserveApprovalStatus.AutoApproved)
                    .Sum(h => (decimal?)h.Amount) ?? 0))
            .ToListAsync(cancellationToken);

        return new PagedResult<ClaimSummaryDto>(items, totalCount, request.Page, request.PageSize);
    }

    private static IQueryable<Claim> ApplyFilters(IQueryable<Claim> claims, ListClaimsQuery request) =>
        claims
            .WhereIf(request.Status is { Length: > 0 }, c => request.Status!.Contains(c.Status))
            .WhereIf(request.DateFrom.HasValue, c => c.LossEvent!.LossDate >= request.DateFrom!.Value)
            .WhereIf(request.DateTo.HasValue, c => c.LossEvent!.LossDate <= request.DateTo!.Value)
            .WhereIf(request.AssignedHandlerId.HasValue, c => c.AssignedHandlerId == request.AssignedHandlerId!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(request.CauseOfLossCode), c => c.LossEvent!.CauseOfLossCode == request.CauseOfLossCode)
            .WhereIf(request.PolicyId.HasValue, c => c.PolicyId == request.PolicyId!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(request.Search), c => c.ClaimNumber.Contains(request.Search!)
                || (c.ClientName != null && c.ClientName.Contains(request.Search!)));
}
