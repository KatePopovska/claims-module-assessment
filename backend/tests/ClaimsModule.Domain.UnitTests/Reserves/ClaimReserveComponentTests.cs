using ClaimsModule.Domain.Enums;
using static ClaimsModule.Domain.UnitTests.TestData;

namespace ClaimsModule.Domain.UnitTests.Reserves;

public class ClaimReserveComponentTests
{
    [Fact]
    public void RecordTransaction_First_IsAnAddFromZero()
    {
        var component = Claim().OpenReserveComponent(ReserveComponentType.Indemnity);

        var transaction = component.RecordTransaction(5000, "Initial estimate", HandlerId, requiresApproval: false);

        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.Equal(ReserveTransactionType.Add, transaction.TransactionType);
        Assert.Equal(1, transaction.ChangeSequence);
        Assert.Equal(0, transaction.PreviousBalance);
        Assert.Equal(5000, transaction.NewBalance);
        Assert.Equal(ReserveApprovalStatus.AutoApproved, transaction.ApprovalStatus);
        Assert.Equal(ReservePostingStatus.Pending, transaction.PostingStatus);
        Assert.Equal($"Reserve:{component.Id}:Change:1", transaction.IdempotencyKey);
        Assert.Equal(component.ClaimId, transaction.ClaimId);
        Assert.Equal(component.Id, transaction.ReserveComponentId);
        Assert.Equal(HandlerId, transaction.SubmittedByUserId);
        Assert.Equal("Initial estimate", transaction.ChangeReason);
        Assert.Equal(5000, component.CurrentAmount);
    }

    [Fact]
    public void RecordTransaction_Increase_IsAnAdjustFromTheApprovedBalance()
    {
        var component = ApprovedReserve(Claim(), ReserveComponentType.Indemnity, 5000);

        var transaction = component.RecordTransaction(2500, null, HandlerId, requiresApproval: false);

        Assert.Equal(ReserveTransactionType.Adjust, transaction.TransactionType);
        Assert.Equal(2, transaction.ChangeSequence);
        Assert.Equal(5000, transaction.PreviousBalance);
        Assert.Equal(7500, transaction.NewBalance);
        Assert.Equal(7500, component.CurrentAmount);
    }

    [Fact]
    public void RecordTransaction_Decrease_IsAReverse()
    {
        var component = ApprovedReserve(Claim(), ReserveComponentType.Indemnity, 5000);

        var transaction = component.RecordTransaction(-2000, null, HandlerId, requiresApproval: false);

        Assert.Equal(ReserveTransactionType.Reverse, transaction.TransactionType);
        Assert.Equal(3000, component.CurrentAmount);
    }

    [Fact]
    public void RecordTransaction_RequiringApproval_IsPendingAndLeavesBalanceUnchanged()
    {
        var component = ApprovedReserve(Claim(), ReserveComponentType.Indemnity, 5000);

        var transaction = component.RecordTransaction(50000, null, HandlerId, requiresApproval: true);

        Assert.Equal(ReserveApprovalStatus.PendingApproval, transaction.ApprovalStatus);
        Assert.Equal(55000, transaction.NewBalance);
        Assert.Equal(5000, component.CurrentAmount);
        Assert.True(component.HasPendingTransaction());
    }

    [Fact]
    public void ApprovedBalance_IgnoresRejectedAndCancelledTransactions()
    {
        var component = ApprovedReserve(Claim(), ReserveComponentType.Expense, 1000);
        component.RecordTransaction(20000, null, HandlerId, requiresApproval: true).Reject(SupervisorId, "Too high", Now);
        component.RecordTransaction(15000, null, HandlerId, requiresApproval: true).Retract();

        Assert.Equal(1000, component.GetApprovedBalance());
        Assert.False(component.HasPendingTransaction());
    }

    [Fact]
    public void ChangeSequence_ContinuesAfterRejectedTransactions()
    {
        var component = Claim().OpenReserveComponent(ReserveComponentType.Expense);
        component.RecordTransaction(20000, null, HandlerId, requiresApproval: true).Reject(SupervisorId, "Too high", Now);

        var resubmitted = component.RecordTransaction(15000, null, HandlerId, requiresApproval: true);

        Assert.Equal(2, resubmitted.ChangeSequence);
        Assert.Equal(0, resubmitted.PreviousBalance);
        Assert.Equal($"Reserve:{component.Id}:Change:2", resubmitted.IdempotencyKey);
    }

    [Fact]
    public void RecalculateCurrentAmount_ReflectsApprovalOfPendingTransaction()
    {
        var component = ApprovedReserve(Claim(), ReserveComponentType.Indemnity, 5000);
        var pending = component.RecordTransaction(50000, null, HandlerId, requiresApproval: true);

        pending.Approve(SupervisorId, Now);
        component.RecalculateCurrentAmount();

        Assert.Equal(55000, component.CurrentAmount);
    }
}
