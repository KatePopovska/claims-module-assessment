using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Reserves;

public class ReserveHistory : BaseAuditableEntity, IClaimChild
{
    public Guid ReserveComponentId { get; set; }
    public ClaimReserveComponent ReserveComponent { get; set; } = null!;

    public Guid ClaimId { get; set; }

    public ReserveTransactionType TransactionType { get; set; }
    public decimal Amount { get; set; }
    public decimal PreviousBalance { get; set; }
    public decimal NewBalance { get; set; }

    public ReserveApprovalStatus ApprovalStatus { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? ChangeReason { get; set; }

    public ReservePostingStatus PostingStatus { get; set; } = ReservePostingStatus.Pending;
    public string? PostingJobId { get; set; }

    public string IdempotencyKey { get; set; } = null!;
    public int ChangeSequence { get; set; }

    public Guid SubmittedByUserId { get; set; }

    public bool IsApproved() => ApprovalStatus is ReserveApprovalStatus.Approved or ReserveApprovalStatus.AutoApproved;

    public bool IsPending() => ApprovalStatus == ReserveApprovalStatus.PendingApproval;

    public void Approve(Guid approvedByUserId, DateTimeOffset now)
    {
        EnsurePending();
        ApprovalStatus = ReserveApprovalStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = now;
    }

    public void Reject(Guid rejectedByUserId, string reason, DateTimeOffset now)
    {
        EnsurePending();
        ApprovalStatus = ReserveApprovalStatus.Rejected;
        RejectedByUserId = rejectedByUserId;
        RejectedAt = now;
        RejectionReason = reason;
        PostingStatus = ReservePostingStatus.Cancelled;
    }

    public void Retract()
    {
        EnsurePending();
        ApprovalStatus = ReserveApprovalStatus.Cancelled;
        PostingStatus = ReservePostingStatus.Cancelled;
    }

    public void MarkPosted(string? postingJobId)
    {
        PostingStatus = ReservePostingStatus.Posted;
        PostingJobId = postingJobId;
    }

    public void MarkPostingFailed() => PostingStatus = ReservePostingStatus.Failed;

    private void EnsurePending()
    {
        if (!IsPending())
        {
            throw new InvalidOperationException($"Reserve transaction {Id} is {ApprovalStatus}, not {ReserveApprovalStatus.PendingApproval}.");
        }
    }
}
