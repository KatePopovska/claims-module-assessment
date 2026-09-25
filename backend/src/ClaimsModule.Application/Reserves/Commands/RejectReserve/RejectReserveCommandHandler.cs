using System.Globalization;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Extensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.RejectReserve;

public class RejectReserveCommandHandler(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : IRequestHandler<RejectReserveCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(RejectReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithReservesAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var transaction = claim.FindReserveTransaction(request.TransactionId)
            ?? throw new NotFoundException(nameof(ReserveHistory), request.TransactionId);

        ReserveRules.CheckRejection(transaction, currentUserService.Role).ThrowIfAny();

        transaction.Reject(currentUserService.GetRequiredUserId(), request.RejectionReason, timeProvider.GetUtcNow());

        var amountText = transaction.Amount.ToString("N2", CultureInfo.InvariantCulture);
        auditLogService.Log(claim, AuditEventType.RESERVE_REJECTED, $"{transaction.ReserveComponent.Component} reserve {transaction.TransactionType} of {amountText} rejected.", oldValue: request.RejectionReason, newValue: ReserveApprovalStatus.Rejected.ToString(), relatedEntityId: transaction.Id, relatedEntityType: nameof(ReserveHistory));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ReserveProjections.ToDto(transaction);
    }
}
