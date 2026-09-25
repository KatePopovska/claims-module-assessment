using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Reserves;

public class ClaimReserveComponent : BaseAuditableEntity, ISoftDelete, IHasConcurrencyToken
{
    public Guid ClaimId { get; set; }
    public Claim Claim { get; set; } = null!;

    public ReserveComponentType Component { get; set; }
    public decimal CurrentAmount { get; set; }
    public ReserveComponentStatus Status { get; set; } = ReserveComponentStatus.Active;
    public string? Notes { get; set; }

    public byte[] RowVer { get; set; } = [];

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<ReserveHistory> History { get; set; } = [];

    public decimal GetApprovedBalance() => History.Where(h => h.IsApproved()).Sum(h => h.Amount);

    public bool HasPendingTransaction() => History.Any(h => h.IsPending());

    public ReserveHistory RecordTransaction(decimal amount, string? changeReason, Guid submittedByUserId, bool requiresApproval)
    {
        var previousBalance = GetApprovedBalance();
        var changeSequence = History.Count == 0 ? 1 : History.Max(h => h.ChangeSequence) + 1;

        var transaction = new ReserveHistory
        {
            Id = SequentialGuid.NewGuid(),
            ReserveComponent = this,
            ReserveComponentId = Id,
            ClaimId = ClaimId,
            TransactionType = History.Count == 0 ? ReserveTransactionType.Add : amount < 0 ? ReserveTransactionType.Reverse : ReserveTransactionType.Adjust,
            Amount = amount,
            PreviousBalance = previousBalance,
            NewBalance = previousBalance + amount,
            ApprovalStatus = requiresApproval ? ReserveApprovalStatus.PendingApproval : ReserveApprovalStatus.AutoApproved,
            ChangeReason = changeReason,
            SubmittedByUserId = submittedByUserId,
            ChangeSequence = changeSequence,
            IdempotencyKey = $"Reserve:{Id}:Change:{changeSequence}"
        };

        History.Add(transaction);
        RecalculateCurrentAmount();

        return transaction;
    }

    public void RecalculateCurrentAmount() => CurrentAmount = GetApprovedBalance();
}
