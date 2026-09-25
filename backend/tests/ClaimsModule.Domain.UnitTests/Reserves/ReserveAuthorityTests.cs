using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Domain.UnitTests.Reserves;

public class ReserveAuthorityTests
{
    [Theory]
    [InlineData(10_000, true)]
    [InlineData(10_000.01, false)]
    [InlineData(-10_000, true)]
    [InlineData(-10_000.01, false)]
    [InlineData(0.01, true)]
    public void IsWithinAutoApprovalLimit_UsesAbsoluteAmount(double amount, bool expected)
    {
        Assert.Equal(expected, ReserveAuthority.IsWithinAutoApprovalLimit((decimal)amount));
    }

    [Theory]
    [InlineData("handler", 5_000, false)]
    [InlineData("supervisor", 100_000, true)]
    [InlineData("supervisor", 100_000.01, false)]
    [InlineData("supervisor", -50_000, true)]
    [InlineData("supervisor", -150_000, false)]
    [InlineData("manager", 100_000.01, true)]
    [InlineData("manager", 10_000_000, true)]
    [InlineData("manager", -150_000, true)]
    [InlineData("MANAGER", 150_000, true)]
    [InlineData(null, 5_000, false)]
    public void CanApprove_FollowsAuthorityThresholds(string? role, double amount, bool expected)
    {
        Assert.Equal(expected, ReserveAuthority.CanApprove(role, (decimal)amount));
    }

    [Theory]
    [InlineData("manager", true)]
    [InlineData("Manager", true)]
    [InlineData("supervisor", false)]
    [InlineData(null, false)]
    public void IsManager_IsCaseInsensitive(string? role, bool expected)
    {
        Assert.Equal(expected, ReserveAuthority.IsManager(role));
    }
}
