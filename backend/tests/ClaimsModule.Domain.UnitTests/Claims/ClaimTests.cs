using ClaimsModule.Domain.Enums;
using static ClaimsModule.Domain.UnitTests.TestData;

namespace ClaimsModule.Domain.UnitTests.Claims;

public class ClaimTests
{
    [Fact]
    public void ChangeStatus_ToClosed_RecordsClosureDetails()
    {
        var claim = Claim(ClaimStatus.Open);

        var change = Assert.Single(claim.ChangeStatus(ClaimStatus.Closed, "Settled", Now));

        Assert.Equal(ClaimStatus.Closed, claim.Status);
        Assert.Equal(Now, claim.ClosedAt);
        Assert.Equal("Settled", claim.ClosureReason);
        Assert.Equal((ClaimStatus.Open, ClaimStatus.Closed, false), (change.From, change.To, change.IsAutomatic));
    }

    [Fact]
    public void ChangeStatus_ToReopened_HopsAutomaticallyToOpenAndClearsClosure()
    {
        var claim = Claim(ClaimStatus.Closed);
        claim.ClosedAt = Now.AddDays(-1);
        claim.ClosureReason = "Settled";

        var changes = claim.ChangeStatus(ClaimStatus.Reopened, "New evidence", Now);

        Assert.Equal(ClaimStatus.Open, claim.Status);
        Assert.Null(claim.ClosedAt);
        Assert.Null(claim.ClosureReason);
        Assert.Collection(changes,
            first => Assert.Equal((ClaimStatus.Closed, ClaimStatus.Reopened, false), (first.From, first.To, first.IsAutomatic)),
            second => Assert.Equal((ClaimStatus.Reopened, ClaimStatus.Open, true), (second.From, second.To, second.IsAutomatic)));
    }

    [Fact]
    public void ChangeStatus_InvalidTransition_Throws()
    {
        var claim = Claim(ClaimStatus.Draft);

        Assert.Throws<InvalidOperationException>(() => claim.ChangeStatus(ClaimStatus.Closed, null, Now));
        Assert.Equal(ClaimStatus.Draft, claim.Status);
    }

    [Fact]
    public void IsLastActiveClaimant_TrueForTheOnlyActiveClaimant()
    {
        var claim = Claim();

        Assert.True(claim.IsLastActiveClaimant(claim.Parties.Single()));
    }

    [Fact]
    public void IsLastActiveClaimant_FalseWhenAnotherActiveClaimantExists()
    {
        var claim = Claim();
        claim.Parties.Add(Claimant());

        Assert.False(claim.IsLastActiveClaimant(claim.Parties.First()));
    }

    [Fact]
    public void IsLastActiveClaimant_FalseForInactiveOrNonClaimantParties()
    {
        var claim = Claim();
        var inactive = Claimant(isActive: false);
        var witness = Claimant();
        witness.PartyRole = PartyRole.Witness;
        claim.Parties.Add(inactive);
        claim.Parties.Add(witness);

        Assert.False(claim.IsLastActiveClaimant(inactive));
        Assert.False(claim.IsLastActiveClaimant(witness));
    }

    [Fact]
    public void OpenReserveComponent_AssignsIdAndAttachesToClaim()
    {
        var claim = Claim();

        var component = claim.OpenReserveComponent(ReserveComponentType.Expense);

        Assert.NotEqual(Guid.Empty, component.Id);
        Assert.Equal(claim.Id, component.ClaimId);
        Assert.Same(claim, component.Claim);
        Assert.Contains(component, claim.ReserveComponents);
        Assert.Equal(ReserveComponentStatus.Active, component.Status);
    }

    [Fact]
    public void GetApprovedReserveTotal_SumsApprovedBalancesIncludingNegativeSubrogation()
    {
        var claim = Claim();
        ApprovedReserve(claim, ReserveComponentType.Indemnity, 25000);
        ApprovedReserve(claim, ReserveComponentType.Expense, 5000);
        ApprovedReserve(claim, ReserveComponentType.SubrogationRecoverable, -8000);
        PendingTransaction(claim, ReserveComponentType.ALAE, 40000);

        Assert.Equal(22000, claim.GetApprovedReserveTotal());
    }

    [Theory]
    [InlineData(9_990_000, 10_000, false)]
    [InlineData(9_990_000, 10_001, true)]
    [InlineData(0, 10_000_001, true)]
    public void WouldExceedReserveLimit_ComparesAgainstTenMillion(decimal approved, decimal amount, bool expected)
    {
        var claim = Claim();
        if (approved > 0)
        {
            ApprovedReserve(claim, ReserveComponentType.Indemnity, approved);
        }

        Assert.Equal(expected, claim.WouldExceedReserveLimit(amount));
    }

    [Fact]
    public void WouldExceedReserveLimit_FalseOnceOverrideIsSet()
    {
        var claim = Claim();
        ApprovedReserve(claim, ReserveComponentType.Indemnity, 9_999_000);

        claim.SetReserveLimitOverride(ManagerId, "Catastrophic loss", Now);

        Assert.False(claim.WouldExceedReserveLimit(50_000));
        Assert.True(claim.ReserveLimitOverride);
        Assert.Equal(ManagerId, claim.ReserveLimitOverrideByUserId);
        Assert.Equal(Now, claim.ReserveLimitOverrideAt);
        Assert.Equal("Catastrophic loss", claim.ReserveLimitOverrideReason);
    }

    [Fact]
    public void FindReserveTransaction_SearchesAcrossComponents()
    {
        var claim = Claim();
        ApprovedReserve(claim, ReserveComponentType.Indemnity, 1000);
        var pending = PendingTransaction(claim, ReserveComponentType.Expense, 20000);

        Assert.Same(pending, claim.FindReserveTransaction(pending.Id));
        Assert.Null(claim.FindReserveTransaction(Guid.NewGuid()));
    }

    [Fact]
    public void FindReserveTransaction_ReturnsNullWhenClaimHasNoReserves()
    {
        Assert.Null(Claim().FindReserveTransaction(Guid.NewGuid()));
    }
}
