using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Extensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Reserves;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.CreateReserve;

public class CreateReserveCommandHandler(
    IClaimRepository claimRepository,
    ReserveTransactionSubmitter submitter) : IRequestHandler<CreateReserveCommand, ReserveSubmissionResultDto>
{
    public async Task<ReserveSubmissionResultDto> Handle(CreateReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithReservesAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        ReserveRules.CheckNewReserve(claim, request.Component, request.Amount).ThrowIfAny();

        var component = claim.OpenReserveComponent(request.Component);

        return await submitter.SubmitAsync(claim, component, request.Amount, request.ChangeReason, cancellationToken);
    }
}
