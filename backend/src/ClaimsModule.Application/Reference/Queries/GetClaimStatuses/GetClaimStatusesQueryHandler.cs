using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Reference.Queries.GetClaimStatuses;

public class GetClaimStatusesQueryHandler : IRequestHandler<GetClaimStatusesQuery, IReadOnlyList<ClaimStatusDto>>
{
    public Task<IReadOnlyList<ClaimStatusDto>> Handle(GetClaimStatusesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<ClaimStatusDto> statuses = Enum.GetValues<ClaimStatus>()
            .Select(s => new ClaimStatusDto(s, ClaimStatusTransitions.GetValidNextStatuses(s)))
            .ToList();

        return Task.FromResult(statuses);
    }
}
