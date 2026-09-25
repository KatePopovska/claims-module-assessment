using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.MarkGlPostingFailed;

public class MarkGlPostingFailedCommandHandler(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService) : IRequestHandler<MarkGlPostingFailedCommand>
{
    public async Task Handle(MarkGlPostingFailedCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithReservesAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var transaction = claim.FindReserveTransaction(request.ReserveHistoryId)
            ?? throw new NotFoundException(nameof(ReserveHistory), request.ReserveHistoryId);

        if (transaction.PostingStatus is ReservePostingStatus.Posted or ReservePostingStatus.Failed)
        {
            return;
        }

        transaction.MarkPostingFailed();

        auditLogService.Log(claim, AuditEventType.GL_POSTING_FAILED, $"GL posting failed after all retries for reserve transaction {transaction.IdempotencyKey}.", newValue: request.FailureReason, relatedEntityId: transaction.Id, relatedEntityType: nameof(ReserveHistory));

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
