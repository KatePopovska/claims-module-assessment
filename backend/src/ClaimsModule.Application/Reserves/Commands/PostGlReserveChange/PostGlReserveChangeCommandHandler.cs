using System.Globalization;
using System.Text.Json;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.PostGlReserveChange;

public class PostGlReserveChangeCommandHandler(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService) : IRequestHandler<PostGlReserveChangeCommand>
{
    private const string DebitAccount = "Change in Outstanding Reserves";
    private const string CreditAccount = "Outstanding Loss Reserves";

    public async Task Handle(PostGlReserveChangeCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithReservesAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var transaction = claim.FindReserveTransaction(request.ReserveHistoryId)
            ?? throw new NotFoundException(nameof(ReserveHistory), request.ReserveHistoryId);

        if (transaction.IdempotencyKey != request.IdempotencyKey)
        {
            throw new InvalidOperationException($"Idempotency key {request.IdempotencyKey} does not match reserve transaction {transaction.Id}.");
        }

        if (transaction.PostingStatus == ReservePostingStatus.Posted)
        {
            return;
        }

        if (!transaction.IsApproved())
        {
            throw new InvalidOperationException($"Reserve transaction {transaction.Id} is {transaction.ApprovalStatus} and cannot be posted.");
        }

        var journal = JsonSerializer.Serialize(new
        {
            debit = DebitAccount,
            credit = CreditAccount,
            amount = transaction.Amount,
            idempotencyKey = transaction.IdempotencyKey
        });

        var amountText = transaction.Amount.ToString("N2", CultureInfo.InvariantCulture);
        auditLogService.Log(claim, AuditEventType.GL_POSTING_SIMULATED, $"Simulated GL posting: DR {DebitAccount} / CR {CreditAccount}, Amount = {amountText}.", newValue: journal, relatedEntityId: transaction.Id, relatedEntityType: nameof(ReserveHistory));

        transaction.MarkPosted(request.JobId);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
