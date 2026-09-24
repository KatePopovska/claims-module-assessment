using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Reference.Queries.SearchPolicies;

public class SearchPoliciesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<SearchPoliciesQuery, IReadOnlyList<PolicySearchResultDto>>
{
    public async Task<IReadOnlyList<PolicySearchResultDto>> Handle(SearchPoliciesQuery request, CancellationToken cancellationToken)
    {
        return await context.Policies
            .AsNoTracking()
            .Where(p => string.IsNullOrWhiteSpace(request.SearchTerm)
                || p.PolicyNumber.Contains(request.SearchTerm)
                || p.ClientName.Contains(request.SearchTerm))
            .OrderBy(p => p.PolicyNumber)
            .Select(p => new PolicySearchResultDto(p.Id, p.PolicyNumber, p.ClientName, p.EffectiveDate, p.ExpirationDate, p.Status, p.CoverageTypes))
            .ToListAsync(cancellationToken);
    }
}
