using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using static ClaimsModule.Domain.UnitTests.TestData;

namespace ClaimsModule.Domain.UnitTests.Claims;

public class ClaimStatusChangePolicyTests
{
    private static StatusChangeEvaluation Evaluate(Claim claim, ClaimStatus target, string? reason = null, string? role = "handler", bool acknowledged = false, bool causeOfLossActive = true) =>
        ClaimStatusChangePolicy.Evaluate(claim, new StatusChangeRequest(target, reason, role, acknowledged), Now, causeOfLossActive);

    private static IEnumerable<string> Fields(StatusChangeEvaluation evaluation) => evaluation.BlockingIssues.Select(i => i.Field);

    [Fact]
    public void InvalidTransition_ReturnsSingleStatusIssueListingValidNextStatuses()
    {
        var evaluation = Evaluate(Claim(ClaimStatus.Draft), ClaimStatus.Closed);

        var issue = Assert.Single(evaluation.BlockingIssues);
        Assert.Equal("Status", issue.Field);
        Assert.Equal("Transition from Draft to Closed is not permitted. Valid next statuses: Open.", issue.Message);
    }

    [Fact]
    public void DraftToOpen_WithValidClaim_HasNoIssues()
    {
        var evaluation = Evaluate(Claim(), ClaimStatus.Open);

        Assert.Empty(evaluation.BlockingIssues);
        Assert.Empty(evaluation.AcknowledgedWarnings);
    }

    [Fact]
    public void Open_RequiresAnActiveClaimant()
    {
        var claim = Claim();
        claim.Parties = [Claimant(isActive: false)];

        Assert.Contains("Parties", Fields(Evaluate(claim, ClaimStatus.Open)));
    }

    [Fact]
    public void Open_ReportsEveryCriticalLossEventIssue()
    {
        var claim = Claim();
        claim.LossEvent!.LossDate = Now.AddDays(1);
        claim.LossEvent.LossDescription = "Too short";

        var fields = Fields(Evaluate(claim, ClaimStatus.Open, causeOfLossActive: false)).ToList();

        Assert.Contains("LossDate", fields);
        Assert.Contains("LossDescription", fields);
        Assert.Contains("CauseOfLossCode", fields);
    }

    [Fact]
    public void Open_RequiresALossEvent()
    {
        var claim = Claim();
        claim.LossEvent = null;

        Assert.Contains("LossEvent", Fields(Evaluate(claim, ClaimStatus.Open)));
    }

    [Fact]
    public void DraftToOpen_LossDateOutsidePolicyPeriod_IsBlockedUntilAcknowledged()
    {
        var claim = Claim();
        claim.LossEvent!.LossDate = claim.Policy!.EffectiveDate.AddDays(-1);

        var blocked = Evaluate(claim, ClaimStatus.Open);
        var acknowledged = Evaluate(claim, ClaimStatus.Open, acknowledged: true);

        Assert.Equal("Warnings", Assert.Single(blocked.BlockingIssues).Field);
        Assert.Empty(acknowledged.BlockingIssues);
        Assert.Equal("Loss date is outside the policy effective period.", Assert.Single(acknowledged.AcknowledgedWarnings));
    }

    [Fact]
    public void DraftToOpen_WithoutPolicy_IsNotBlockedByWarnings()
    {
        var claim = Claim();
        claim.PolicyId = null;
        claim.Policy = null;

        Assert.Empty(Evaluate(claim, ClaimStatus.Open).BlockingIssues);
    }

    [Fact]
    public void UnderInvestigationToOpen_DoesNotRequireWarningAcknowledgement()
    {
        var claim = Claim(ClaimStatus.UnderInvestigation);
        claim.LossEvent!.LossDate = claim.Policy!.EffectiveDate.AddDays(-1);

        Assert.Empty(Evaluate(claim, ClaimStatus.Open).BlockingIssues);
    }

    [Fact]
    public void ReopenedToOpen_SkipsOpenEntryChecks()
    {
        var claim = Claim(ClaimStatus.Reopened);
        claim.Parties = [];
        claim.LossEvent = null;

        Assert.Empty(Evaluate(claim, ClaimStatus.Open).BlockingIssues);
    }

    [Fact]
    public void PendingPayment_WithoutReserves_IsBlocked()
    {
        Assert.Contains("Reserves", Fields(Evaluate(Claim(ClaimStatus.Open), ClaimStatus.PendingPayment)));
    }

    [Fact]
    public void PendingPayment_WithCurrentApprovedBalance_IsAllowed()
    {
        var claim = Claim(ClaimStatus.Open);
        ApprovedReserve(claim, ReserveComponentType.Indemnity, 5000);

        Assert.Empty(Evaluate(claim, ClaimStatus.PendingPayment).BlockingIssues);
    }

    [Fact]
    public void PendingPayment_ReserveReversedToZero_IsBlocked()
    {
        var claim = Claim(ClaimStatus.Open);
        var component = ApprovedReserve(claim, ReserveComponentType.Indemnity, 5000);
        component.RecordTransaction(-5000, null, HandlerId, requiresApproval: false);

        Assert.Contains("Reserves", Fields(Evaluate(claim, ClaimStatus.PendingPayment)));
    }

    [Fact]
    public void PendingPayment_OnlyPendingReserve_IsBlocked()
    {
        var claim = Claim(ClaimStatus.Open);
        PendingTransaction(claim, ReserveComponentType.Indemnity, 50000);

        Assert.Contains("Reserves", Fields(Evaluate(claim, ClaimStatus.PendingPayment)));
    }

    [Fact]
    public void PendingPayment_ClosedComponent_DoesNotCount()
    {
        var claim = Claim(ClaimStatus.Open);
        ApprovedReserve(claim, ReserveComponentType.Indemnity, 5000).Status = ReserveComponentStatus.Closed;

        Assert.Contains("Reserves", Fields(Evaluate(claim, ClaimStatus.PendingPayment)));
    }

    [Fact]
    public void Closed_WithPendingReserveTransaction_IsBlocked()
    {
        var claim = Claim(ClaimStatus.Open);
        PendingTransaction(claim, ReserveComponentType.Indemnity, 50000);

        Assert.Contains("Reserves", Fields(Evaluate(claim, ClaimStatus.Closed, reason: "Settled")));
    }

    [Fact]
    public void Closed_WithOpenReserves_RequiresJustification()
    {
        var claim = Claim(ClaimStatus.Open);
        ApprovedReserve(claim, ReserveComponentType.Indemnity, 5000);

        Assert.Contains("Reason", Fields(Evaluate(claim, ClaimStatus.Closed)));
        Assert.Empty(Evaluate(claim, ClaimStatus.Closed, reason: "Settled with open reserve").BlockingIssues);
    }

    [Fact]
    public void Closed_WithoutReserves_NeedsNoReason()
    {
        Assert.Empty(Evaluate(Claim(ClaimStatus.Open), ClaimStatus.Closed).BlockingIssues);
    }

    [Theory]
    [InlineData(ClaimStatus.Open, ClaimStatus.Withdrawn)]
    [InlineData(ClaimStatus.Closed, ClaimStatus.Reopened)]
    public void ReasonIsRequired(ClaimStatus from, ClaimStatus to)
    {
        var evaluation = Evaluate(Claim(from), to, reason: " ", role: "supervisor");

        Assert.Equal("Reason", Assert.Single(evaluation.BlockingIssues).Field);
    }

    [Fact]
    public void Reopen_ByHandler_IsBlockedByRole()
    {
        var evaluation = Evaluate(Claim(ClaimStatus.Closed), ClaimStatus.Reopened, reason: "New evidence", role: "handler");

        Assert.Equal("Role", Assert.Single(evaluation.BlockingIssues).Field);
    }

    [Fact]
    public void UnknownRole_IsBlocked()
    {
        Assert.Contains("Role", Fields(Evaluate(Claim(), ClaimStatus.Open, role: "guest")));
    }
}
