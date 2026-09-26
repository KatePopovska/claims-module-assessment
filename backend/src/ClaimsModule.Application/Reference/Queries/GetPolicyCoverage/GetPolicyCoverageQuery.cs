using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Reference.Queries.GetPolicyCoverage;

public record GetPolicyCoverageQuery(Guid PolicyId) : IRequest<PolicyCoverageDto>;

public record PolicyCoverageDto(Guid PolicyId, string PolicyNumber, PolicyStatus Status, DateTimeOffset EffectiveDate, DateTimeOffset ExpirationDate, IReadOnlyList<string> CoverageTypes);
