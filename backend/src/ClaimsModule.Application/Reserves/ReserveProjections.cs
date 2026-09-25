using System.Linq.Expressions;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Reserves;

internal static class ReserveProjections
{
    public static readonly Expression<Func<ReserveHistory, ReserveTransactionDto>> Transaction = h => new ReserveTransactionDto(
        h.Id,
        h.ReserveComponentId,
        h.ReserveComponent.Component,
        h.TransactionType,
        h.Amount,
        h.PreviousBalance,
        h.NewBalance,
        h.ApprovalStatus,
        h.PostingStatus,
        h.ChangeSequence,
        h.ChangeReason,
        h.SubmittedByUserId,
        h.ApprovedByUserId,
        h.ApprovedAt,
        h.RejectedByUserId,
        h.RejectedAt,
        h.RejectionReason,
        h.CreatedAt);

    private static readonly Func<ReserveHistory, ReserveTransactionDto> CompiledTransaction = Transaction.Compile();

    public static ReserveTransactionDto ToDto(ReserveHistory transaction) => CompiledTransaction(transaction);
}
