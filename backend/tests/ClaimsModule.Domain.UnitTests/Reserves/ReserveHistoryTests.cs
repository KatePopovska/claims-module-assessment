using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;
using static ClaimsModule.Domain.UnitTests.TestData;

namespace ClaimsModule.Domain.UnitTests.Reserves;

public class ReserveHistoryTests
{
    [Fact]
    public void Approve_SetsApproverAndTimestamp()
    {
        var transaction = PendingTransaction(Claim(), ReserveComponentType.Indemnity, 50000);

        transaction.Approve(SupervisorId, Now);

        Assert.Equal(ReserveApprovalStatus.Approved, transaction.ApprovalStatus);
        Assert.Equal(SupervisorId, transaction.ApprovedByUserId);
        Assert.Equal(Now, transaction.ApprovedAt);
        Assert.True(transaction.IsApproved());
        Assert.Equal(ReservePostingStatus.Pending, transaction.PostingStatus);
    }

    [Fact]
    public void Reject_RecordsReasonAndCancelsPosting()
    {
        var transaction = PendingTransaction(Claim(), ReserveComponentType.Indemnity, 50000);

        transaction.Reject(SupervisorId, "Insufficient evidence", Now);

        Assert.Equal(ReserveApprovalStatus.Rejected, transaction.ApprovalStatus);
        Assert.Equal(SupervisorId, transaction.RejectedByUserId);
        Assert.Equal(Now, transaction.RejectedAt);
        Assert.Equal("Insufficient evidence", transaction.RejectionReason);
        Assert.Equal(ReservePostingStatus.Cancelled, transaction.PostingStatus);
    }

    [Fact]
    public void Retract_CancelsTransactionAndPosting()
    {
        var transaction = PendingTransaction(Claim(), ReserveComponentType.Indemnity, 50000);

        transaction.Retract();

        Assert.Equal(ReserveApprovalStatus.Cancelled, transaction.ApprovalStatus);
        Assert.Equal(ReservePostingStatus.Cancelled, transaction.PostingStatus);
    }

    [Fact]
    public void StateChanges_OnNonPendingTransaction_Throw()
    {
        var transaction = ApprovedReserve(Claim(), ReserveComponentType.Indemnity, 5000).History.Single();

        Assert.Throws<InvalidOperationException>(() => transaction.Approve(SupervisorId, Now));
        Assert.Throws<InvalidOperationException>(() => transaction.Reject(SupervisorId, "No", Now));
        Assert.Throws<InvalidOperationException>(transaction.Retract);
    }

    [Fact]
    public void MarkPosted_StoresJobId()
    {
        var transaction = ApprovedReserve(Claim(), ReserveComponentType.Indemnity, 5000).History.Single();

        transaction.MarkPosted("42");

        Assert.Equal(ReservePostingStatus.Posted, transaction.PostingStatus);
        Assert.Equal("42", transaction.PostingJobId);
    }

    [Fact]
    public void MarkPostingFailed_SetsFailed()
    {
        var transaction = ApprovedReserve(Claim(), ReserveComponentType.Indemnity, 5000).History.Single();

        transaction.MarkPostingFailed();

        Assert.Equal(ReservePostingStatus.Failed, transaction.PostingStatus);
    }

    [Fact]
    public void ResetPostingForRetry_FromFailed_ReturnsToPendingAndClearsJobId()
    {
        var transaction = ApprovedReserve(Claim(), ReserveComponentType.Indemnity, 5000).History.Single();
        transaction.PostingJobId = "42";
        transaction.MarkPostingFailed();

        transaction.ResetPostingForRetry();

        Assert.Equal(ReservePostingStatus.Pending, transaction.PostingStatus);
        Assert.Null(transaction.PostingJobId);
    }

    [Theory]
    [InlineData(ReservePostingStatus.Pending)]
    [InlineData(ReservePostingStatus.Posted)]
    [InlineData(ReservePostingStatus.Cancelled)]
    public void ResetPostingForRetry_WhenNotFailed_Throws(ReservePostingStatus status)
    {
        var transaction = ApprovedReserve(Claim(), ReserveComponentType.Indemnity, 5000).History.Single();
        transaction.PostingStatus = status;

        Assert.Throws<InvalidOperationException>(transaction.ResetPostingForRetry);
        Assert.Equal(status, transaction.PostingStatus);
    }

    [Theory]
    [InlineData(ReservePostingStatus.Failed, true)]
    [InlineData(ReservePostingStatus.Pending, false)]
    [InlineData(ReservePostingStatus.Posted, false)]
    public void CheckPostingRetry_AllowsOnlyFailedPostings(ReservePostingStatus status, bool allowed)
    {
        var transaction = ApprovedReserve(Claim(), ReserveComponentType.Indemnity, 5000).History.Single();
        transaction.PostingStatus = status;

        Assert.Equal(allowed, ReserveRules.CheckPostingRetry(transaction).Count == 0);
    }
}
