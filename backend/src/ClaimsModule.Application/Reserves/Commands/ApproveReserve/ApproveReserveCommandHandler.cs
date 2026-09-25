using System.Globalization;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Extensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.ApproveReserve;

public class ApproveReserveCommandHandler(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService,
    IGlPostingScheduler glPostingScheduler,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : IRequestHandler<ApproveReserveCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(ApproveReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithReservesAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var transaction = claim.FindReserveTransaction(request.TransactionId)
            ?? throw new NotFoundException(nameof(ReserveHistory), request.TransactionId);

        ReserveRules.CheckApproval(claim, transaction, currentUserService.Role, currentUserService.UserId).ThrowIfAny();

        transaction.Approve(currentUserService.GetRequiredUserId(), timeProvider.GetUtcNow());
        transaction.ReserveComponent.RecalculateCurrentAmount();

        var amountText = transaction.Amount.ToString("N2", CultureInfo.InvariantCulture);
        auditLogService.Log(claim, AuditEventType.RESERVE_APPROVED, $"{transaction.ReserveComponent.Component} reserve {transaction.TransactionType} of {amountText} approved.", oldValue: ReserveApprovalStatus.PendingApproval.ToString(), newValue: ReserveApprovalStatus.Approved.ToString(), relatedEntityId: transaction.Id, relatedEntityType: nameof(ReserveHistory));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        glPostingScheduler.Enqueue(transaction);

        return ReserveProjections.ToDto(transaction);
    }
}
