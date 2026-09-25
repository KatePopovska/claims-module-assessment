using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Extensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.SetReserveLimitOverride;

public class SetReserveLimitOverrideCommandHandler(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : IRequestHandler<SetReserveLimitOverrideCommand>
{
    public async Task Handle(SetReserveLimitOverrideCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithReservesAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        ReserveRules.CheckReserveLimitOverride(claim, currentUserService.Role).ThrowIfAny();

        claim.SetReserveLimitOverride(currentUserService.GetRequiredUserId(), request.Reason, timeProvider.GetUtcNow());

        auditLogService.Log(claim, AuditEventType.RESERVE_OVERRIDE_SET, "Reserve limit override set by Manager.", newValue: request.Reason);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
