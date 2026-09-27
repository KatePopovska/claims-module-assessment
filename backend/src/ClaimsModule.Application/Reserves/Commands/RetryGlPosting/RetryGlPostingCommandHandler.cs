using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Extensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.RetryGlPosting;

public class RetryGlPostingCommandHandler(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService,
    IGlPostingScheduler glPostingScheduler) : IRequestHandler<RetryGlPostingCommand, ReserveTransactionDto>
{
    public async Task<ReserveTransactionDto> Handle(RetryGlPostingCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithReservesAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var transaction = claim.FindReserveTransaction(request.TransactionId)
            ?? throw new NotFoundException(nameof(ReserveHistory), request.TransactionId);

        ReserveRules.CheckPostingRetry(transaction).ThrowIfAny();

        transaction.ResetPostingForRetry();

        auditLogService.Log(claim, AuditEventType.GL_POSTING_RETRIED, $"GL posting retry requested for reserve transaction {transaction.IdempotencyKey}.", oldValue: ReservePostingStatus.Failed.ToString(), newValue: ReservePostingStatus.Pending.ToString(), relatedEntityId: transaction.Id, relatedEntityType: nameof(ReserveHistory));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        glPostingScheduler.Enqueue(transaction);

        return ReserveProjections.ToDto(transaction);
    }
}
