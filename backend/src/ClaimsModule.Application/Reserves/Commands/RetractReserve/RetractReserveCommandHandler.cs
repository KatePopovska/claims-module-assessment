using System.Globalization;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Extensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.RetractReserve;

public class RetractReserveCommandHandler(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService) : IRequestHandler<RetractReserveCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(RetractReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithReservesAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var transaction = claim.FindReserveTransaction(request.TransactionId)
            ?? throw new NotFoundException(nameof(ReserveHistory), request.TransactionId);

        ReserveRules.CheckRetraction(transaction, currentUserService.UserId).ThrowIfAny();

        transaction.Retract();

        var amountText = transaction.Amount.ToString("N2", CultureInfo.InvariantCulture);
        auditLogService.Log(claim, AuditEventType.RESERVE_RETRACTED, $"{transaction.ReserveComponent.Component} reserve {transaction.TransactionType} of {amountText} retracted by submitter.", oldValue: ReserveApprovalStatus.PendingApproval.ToString(), newValue: ReserveApprovalStatus.Cancelled.ToString(), relatedEntityId: transaction.Id, relatedEntityType: nameof(ReserveHistory));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ReserveProjections.ToDto(transaction);
    }
}
