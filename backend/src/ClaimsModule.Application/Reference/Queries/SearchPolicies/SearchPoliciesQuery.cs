using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Reference.Queries.SearchPolicies;

public record SearchPoliciesQuery(string? SearchTerm) : IRequest<IReadOnlyList<PolicySearchResultDto>>;

public record PolicySearchResultDto(Guid Id, string PolicyNumber, string ClientName, DateTimeOffset EffectiveDate, DateTimeOffset ExpirationDate, PolicyStatus Status, string CoverageTypes);
