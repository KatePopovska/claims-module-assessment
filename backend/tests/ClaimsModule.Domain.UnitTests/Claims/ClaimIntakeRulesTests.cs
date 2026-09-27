using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using static ClaimsModule.Domain.UnitTests.TestData;

namespace ClaimsModule.Domain.UnitTests.Claims;

public class ClaimIntakeRulesTests
{
    private static ClaimsModule.Domain.Claims.Claim CompleteClaim()
    {
        var claim = Claim();
        claim.RiskObjects.Add(new ClaimRiskObject { Id = Guid.NewGuid(), AssetType = AssetType.Property, AssetDescription = "Kitchen" });
        return claim;
    }

    [Fact]
    public void GetWarnings_CompleteClaimWithinPolicyPeriod_ReturnsNone()
    {
        Assert.Empty(ClaimIntakeRules.GetWarnings(CompleteClaim()));
    }

    [Fact]
    public void GetWarnings_WithoutPolicy_ReturnsPolicyUnknownOnly()
    {
        var claim = CompleteClaim();
        claim.PolicyId = null;
        claim.Policy = null;

        Assert.Equal([ClaimIntakeRules.PolicyUnknownWarning], ClaimIntakeRules.GetWarnings(claim));
    }

    [Theory]
    [InlineData(-400)]
    [InlineData(400)]
    public void GetWarnings_LossDateOutsidePolicyPeriod_ReturnsWarning(int daysFromNow)
    {
        var claim = CompleteClaim();
        claim.LossEvent!.LossDate = Now.AddDays(daysFromNow);

        Assert.Equal([ClaimIntakeRules.LossDateOutsidePolicyWarning], ClaimIntakeRules.GetWarnings(claim));
    }

    [Fact]
    public void GetWarnings_WithoutRiskObjects_ReturnsWarning()
    {
        Assert.Equal([ClaimIntakeRules.NoRiskObjectsWarning], ClaimIntakeRules.GetWarnings(Claim()));
    }

    [Fact]
    public void GetWarnings_CombinesIndependentWarnings()
    {
        var claim = Claim();
        claim.PolicyId = null;
        claim.Policy = null;

        Assert.Equal([ClaimIntakeRules.PolicyUnknownWarning, ClaimIntakeRules.NoRiskObjectsWarning], ClaimIntakeRules.GetWarnings(claim));
    }
}
