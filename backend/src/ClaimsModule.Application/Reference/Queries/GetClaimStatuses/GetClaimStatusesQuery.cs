using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Reference.Queries.GetClaimStatuses;

public record GetClaimStatusesQuery : IRequest<IReadOnlyList<ClaimStatusDto>>;

public record ClaimStatusDto(ClaimStatus Status, IReadOnlyCollection<ClaimStatus> ValidNextStatuses);
