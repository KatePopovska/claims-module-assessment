using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Extensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Reserves;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.AdjustReserve;

public class AdjustReserveCommandHandler(
    IClaimRepository claimRepository,
    ReserveTransactionSubmitter submitter) : IRequestHandler<AdjustReserveCommand, ReserveSubmissionResultDto>
{
    public async Task<ReserveSubmissionResultDto> Handle(AdjustReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithReservesAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var component = claim.ReserveComponents.FirstOrDefault(rc => rc.Id == request.ReserveComponentId)
            ?? throw new NotFoundException(nameof(ClaimReserveComponent), request.ReserveComponentId);

        ReserveRules.CheckAdjustment(claim, component, request.Amount).ThrowIfAny();

        return await submitter.SubmitAsync(claim, component, request.Amount, request.ChangeReason, cancellationToken);
    }
}
