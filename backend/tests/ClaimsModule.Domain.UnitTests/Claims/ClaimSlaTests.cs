using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using static ClaimsModule.Domain.UnitTests.TestData;

namespace ClaimsModule.Domain.UnitTests.Claims;

public class ClaimSlaTests
{
    private static readonly Func<Claim, bool> IsBreachDue = ClaimSla.IsBreachDue(Now).Compile();

    private static Claim ClaimLastTouched(TimeSpan ago, ClaimStatus status = ClaimStatus.Open, bool neverUpdated = false)
    {
        var claim = Claim(status);
        claim.CreatedAt = Now - ago - TimeSpan.FromDays(1);
        claim.UpdatedAt = Now - ago;

        if (neverUpdated)
        {
            claim.CreatedAt = Now - ago;
            claim.UpdatedAt = null;
        }

        return claim;
    }

    private static void AddBreachEntry(Claim claim, TimeSpan ago) =>
        claim.AuditLogEntries.Add(new ClaimAuditLog { EventType = AuditEventType.SLA_BREACH_DETECTED, CreatedAt = Now - ago, Description = ClaimSla.BreachDescription });

    [Theory]
    [InlineData(ClaimStatus.Draft)]
    [InlineData(ClaimStatus.Open)]
    public void StaleDraftOrOpenClaim_IsDue(ClaimStatus status)
    {
        Assert.True(IsBreachDue(ClaimLastTouched(TimeSpan.FromHours(49), status)));
    }

    [Theory]
    [InlineData(ClaimStatus.UnderInvestigation)]
    [InlineData(ClaimStatus.PendingPayment)]
    [InlineData(ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Withdrawn)]
    public void OtherStatuses_AreNotMonitored(ClaimStatus status)
    {
        Assert.False(IsBreachDue(ClaimLastTouched(TimeSpan.FromDays(10), status)));
    }

    [Fact]
    public void ClaimUpdatedExactlyFortyEightHoursAgo_IsNotYetDue()
    {
        Assert.False(IsBreachDue(ClaimLastTouched(TimeSpan.FromHours(48))));
        Assert.True(IsBreachDue(ClaimLastTouched(TimeSpan.FromHours(48) + TimeSpan.FromTicks(1))));
    }

    [Fact]
    public void NeverUpdatedClaim_UsesCreatedAt()
    {
        Assert.True(IsBreachDue(ClaimLastTouched(TimeSpan.FromHours(49), neverUpdated: true)));
        Assert.False(IsBreachDue(ClaimLastTouched(TimeSpan.FromHours(47), neverUpdated: true)));
    }

    [Fact]
    public void RecentUpdateAfterOldCreation_IsNotDue()
    {
        var claim = Claim();
        claim.CreatedAt = Now.AddDays(-30);
        claim.UpdatedAt = Now.AddHours(-1);

        Assert.False(IsBreachDue(claim));
    }

    [Fact]
    public void BreachRecordedWithinLast24Hours_SuppressesANewOne()
    {
        var claim = ClaimLastTouched(TimeSpan.FromDays(5));
        AddBreachEntry(claim, TimeSpan.FromHours(23));

        Assert.False(IsBreachDue(claim));
    }

    [Fact]
    public void BreachRecordedAtLeast24HoursAgo_AllowsANewOne()
    {
        var claim = ClaimLastTouched(TimeSpan.FromDays(5));
        AddBreachEntry(claim, TimeSpan.FromHours(24));
        AddBreachEntry(claim, TimeSpan.FromHours(48));

        Assert.True(IsBreachDue(claim));
    }

    [Fact]
    public void OtherRecentAuditEvents_DoNotSuppressABreach()
    {
        var claim = ClaimLastTouched(TimeSpan.FromDays(5));
        claim.AuditLogEntries.Add(new ClaimAuditLog { EventType = AuditEventType.PARTY_ADDED, CreatedAt = Now.AddHours(-1), Description = "Party added." });

        Assert.True(IsBreachDue(claim));
    }
}
