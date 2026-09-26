using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Reference;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Reference.Queries.GetPolicyCoverage;

public class GetPolicyCoverageQueryHandler(IApplicationDbContext context) : IRequestHandler<GetPolicyCoverageQuery, PolicyCoverageDto>
{
    public async Task<PolicyCoverageDto> Handle(GetPolicyCoverageQuery request, CancellationToken cancellationToken)
    {
        var policy = await context.Policies
            .AsNoTracking()
            .Where(p => p.Id == request.PolicyId)
            .Select(p => new { p.Id, p.PolicyNumber, p.Status, p.EffectiveDate, p.ExpirationDate, p.CoverageTypes })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Policy), request.PolicyId);

        var coverageTypes = policy.CoverageTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new PolicyCoverageDto(policy.Id, policy.PolicyNumber, policy.Status, policy.EffectiveDate, policy.ExpirationDate, coverageTypes);
    }
}
