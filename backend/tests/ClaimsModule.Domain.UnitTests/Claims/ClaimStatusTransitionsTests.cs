using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.UnitTests.Claims;

public class ClaimStatusTransitionsTests
{
    [Theory]
    [InlineData(ClaimStatus.Draft, ClaimStatus.Open)]
    [InlineData(ClaimStatus.Open, ClaimStatus.UnderInvestigation)]
    [InlineData(ClaimStatus.Open, ClaimStatus.PendingPayment)]
    [InlineData(ClaimStatus.Open, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Open, ClaimStatus.Withdrawn)]
    [InlineData(ClaimStatus.UnderInvestigation, ClaimStatus.Open)]
    [InlineData(ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment)]
    [InlineData(ClaimStatus.UnderInvestigation, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn)]
    [InlineData(ClaimStatus.PendingPayment, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Closed, ClaimStatus.Reopened)]
    [InlineData(ClaimStatus.Reopened, ClaimStatus.Open)]
    public void IsValid_ReturnsTrue_ForEveryFrsTransition(ClaimStatus from, ClaimStatus to)
    {
        Assert.True(ClaimStatusTransitions.IsValid(from, to));
    }

    [Theory]
    [InlineData(ClaimStatus.Draft, ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Open, ClaimStatus.Draft)]
    [InlineData(ClaimStatus.PendingPayment, ClaimStatus.Open)]
    [InlineData(ClaimStatus.Closed, ClaimStatus.Open)]
    [InlineData(ClaimStatus.Withdrawn, ClaimStatus.Open)]
    [InlineData(ClaimStatus.Open, ClaimStatus.Open)]
    public void IsValid_ReturnsFalse_ForTransitionsOutsideTheTable(ClaimStatus from, ClaimStatus to)
    {
        Assert.False(ClaimStatusTransitions.IsValid(from, to));
    }

    [Fact]
    public void GetValidNextStatuses_ReturnsAllTargetsForOpen()
    {
        var next = ClaimStatusTransitions.GetValidNextStatuses(ClaimStatus.Open);

        Assert.Equal([ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment, ClaimStatus.Closed, ClaimStatus.Withdrawn], next);
    }

    [Fact]
    public void GetValidNextStatuses_ReturnsNothingForWithdrawn()
    {
        Assert.Empty(ClaimStatusTransitions.GetValidNextStatuses(ClaimStatus.Withdrawn));
    }

    [Theory]
    [InlineData("handler")]
    [InlineData("supervisor")]
    [InlineData("manager")]
    public void IsPermittedFor_AllowsEveryRoleToOpenADraft(string role)
    {
        Assert.True(ClaimStatusTransitions.IsPermittedFor(ClaimStatus.Draft, ClaimStatus.Open, role));
    }

    [Theory]
    [InlineData("handler", false)]
    [InlineData("supervisor", true)]
    [InlineData("manager", true)]
    [InlineData("Supervisor", true)]
    public void IsPermittedFor_RestrictsReopenToSupervisorOrManager(string role, bool expected)
    {
        Assert.Equal(expected, ClaimStatusTransitions.IsPermittedFor(ClaimStatus.Closed, ClaimStatus.Reopened, role));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("guest")]
    public void IsPermittedFor_RejectsMissingOrUnknownRoles(string? role)
    {
        Assert.False(ClaimStatusTransitions.IsPermittedFor(ClaimStatus.Draft, ClaimStatus.Open, role));
    }

    [Fact]
    public void IsPermittedFor_ReturnsFalseForInvalidTransition()
    {
        Assert.False(ClaimStatusTransitions.IsPermittedFor(ClaimStatus.Draft, ClaimStatus.Closed, "manager"));
    }
}
