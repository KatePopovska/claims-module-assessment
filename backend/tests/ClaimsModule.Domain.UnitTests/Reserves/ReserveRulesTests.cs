using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;
using static ClaimsModule.Domain.UnitTests.TestData;

namespace ClaimsModule.Domain.UnitTests.Reserves;

public class ReserveRulesTests
{
    private static IEnumerable<string> Fields(IEnumerable<BusinessRuleViolation> violations) => violations.Select(v => v.Field);

    [Fact]
    public void CheckNewReserve_ValidRequest_HasNoViolations()
    {
        Assert.Empty(ReserveRules.CheckNewReserve(Claim(), ReserveComponentType.Indemnity, 5000));
    }

    [Fact]
    public void CheckNewReserve_RequiresLinkedPolicy()
    {
        var claim = Claim();
        claim.PolicyId = null;

        var violation = Assert.Single(ReserveRules.CheckNewReserve(claim, ReserveComponentType.Indemnity, 5000));

        Assert.Equal("PolicyId", violation.Field);
        Assert.Equal("No policy linked. Policy must be associated before reserves can be set.", violation.Message);
    }

    [Fact]
    public void CheckNewReserve_RejectsSecondComponentOfSameType()
    {
        var claim = Claim();
        ApprovedReserve(claim, ReserveComponentType.Indemnity, 5000);

        Assert.Equal(["Component"], Fields(ReserveRules.CheckNewReserve(claim, ReserveComponentType.Indemnity, 100)));
    }

    [Theory]
    [InlineData(ReserveComponentType.Indemnity, 0, false)]
    [InlineData(ReserveComponentType.Expense, -5, false)]
    [InlineData(ReserveComponentType.ALAE, 1, true)]
    [InlineData(ReserveComponentType.SubrogationRecoverable, -8000, true)]
    [InlineData(ReserveComponentType.SubrogationRecoverable, 0, false)]
    public void CheckNewReserve_AmountMustBePositiveExceptSubrogation(ReserveComponentType component, int amount, bool valid)
    {
        var violations = ReserveRules.CheckNewReserve(Claim(), component, amount);

        if (valid)
        {
            Assert.Empty(violations);
        }
        else
        {
            Assert.Equal("Reserve amount must be greater than zero.", Assert.Single(violations).Message);
        }
    }

    [Fact]
    public void CheckAdjustment_ValidChange_HasNoViolations()
    {
        var claim = Claim();
        var component = ApprovedReserve(claim, ReserveComponentType.Indemnity, 5000);

        Assert.Empty(ReserveRules.CheckAdjustment(claim, component, -5000));
    }

    [Fact]
    public void CheckAdjustment_RejectsZeroDelta()
    {
        var claim = Claim();
        var component = ApprovedReserve(claim, ReserveComponentType.Indemnity, 5000);

        Assert.Equal(["Amount"], Fields(ReserveRules.CheckAdjustment(claim, component, 0)));
    }

    [Fact]
    public void CheckAdjustment_BlockedWhileComponentHasPendingTransaction()
    {
        var claim = Claim();
        var component = ApprovedReserve(claim, ReserveComponentType.Indemnity, 5000);
        component.RecordTransaction(50000, null, HandlerId, requiresApproval: true);

        Assert.Equal(["Component"], Fields(ReserveRules.CheckAdjustment(claim, component, 10)));
    }

    [Fact]
    public void CheckAdjustment_CannotTakeNonSubrogationBalanceBelowZero()
    {
        var claim = Claim();
        var component = ApprovedReserve(claim, ReserveComponentType.Expense, 5000);

        Assert.Equal(["Amount"], Fields(ReserveRules.CheckAdjustment(claim, component, -5000.01m)));
    }

    [Fact]
    public void CheckAdjustment_SubrogationMayGoFurtherNegative()
    {
        var claim = Claim();
        var component = ApprovedReserve(claim, ReserveComponentType.SubrogationRecoverable, -8000);

        Assert.Empty(ReserveRules.CheckAdjustment(claim, component, -2000));
    }

    [Fact]
    public void CheckAdjustment_RequiresLinkedPolicy()
    {
        var claim = Claim();
        var component = ApprovedReserve(claim, ReserveComponentType.Indemnity, 5000);
        claim.PolicyId = null;

        Assert.Equal(["PolicyId"], Fields(ReserveRules.CheckAdjustment(claim, component, 100)));
    }

    [Fact]
    public void GetSubmissionWarnings_WarnsWhenTenMillionLimitWouldBeExceeded()
    {
        var claim = Claim();
        ApprovedReserve(claim, ReserveComponentType.Indemnity, 9_995_000);

        Assert.Equal([ReserveRules.ReserveLimitWarning], ReserveRules.GetSubmissionWarnings(claim, 6000));
        Assert.Empty(ReserveRules.GetSubmissionWarnings(claim, 5000));
    }

    [Fact]
    public void GetSubmissionWarnings_NoWarningWithOverride()
    {
        var claim = Claim();
        ApprovedReserve(claim, ReserveComponentType.Indemnity, 9_995_000);
        claim.SetReserveLimitOverride(ManagerId, "Approved", Now);

        Assert.Empty(ReserveRules.GetSubmissionWarnings(claim, 6000));
    }

    [Fact]
    public void CheckApproval_SupervisorApprovingSomeoneElsesTransaction_IsAllowed()
    {
        var claim = Claim();
        var pending = PendingTransaction(claim, ReserveComponentType.Indemnity, 50000);

        Assert.Empty(ReserveRules.CheckApproval(claim, pending, "supervisor", SupervisorId));
    }

    [Fact]
    public void CheckApproval_RoleWithoutAuthority_IsRejected()
    {
        var claim = Claim();
        var pending = PendingTransaction(claim, ReserveComponentType.Indemnity, 150000);

        var violation = Assert.Single(ReserveRules.CheckApproval(claim, pending, "supervisor", SupervisorId));

        Assert.Equal("Role", violation.Field);
        Assert.Equal("Your role does not have authority to approve this reserve amount.", violation.Message);
    }

    [Fact]
    public void CheckApproval_SelfApproval_IsRejected()
    {
        var claim = Claim();
        var pending = PendingTransaction(claim, ReserveComponentType.Indemnity, 50000, submittedBy: SupervisorId);

        var violation = Assert.Single(ReserveRules.CheckApproval(claim, pending, "supervisor", SupervisorId));

        Assert.Equal("Approver", violation.Field);
        Assert.Equal("Self-approval is not permitted.", violation.Message);
    }

    [Fact]
    public void CheckApproval_OverTenMillionWithoutOverride_IsRejected()
    {
        var claim = Claim();
        ApprovedReserve(claim, ReserveComponentType.Indemnity, 9_995_000);
        var pending = PendingTransaction(claim, ReserveComponentType.ALAE, 6000);

        Assert.Equal(["Amount"], Fields(ReserveRules.CheckApproval(claim, pending, "manager", ManagerId)));

        claim.SetReserveLimitOverride(ManagerId, "Approved", Now);
        Assert.Empty(ReserveRules.CheckApproval(claim, pending, "manager", ManagerId));
    }

    [Fact]
    public void CheckApproval_NonPendingTransaction_ReturnsOnlyStatusViolation()
    {
        var claim = Claim();
        var approved = ApprovedReserve(claim, ReserveComponentType.Indemnity, 5000).History.Single();

        Assert.Equal(["ApprovalStatus"], Fields(ReserveRules.CheckApproval(claim, approved, "handler", HandlerId)));
    }

    [Theory]
    [InlineData("handler", 50_000, false)]
    [InlineData("supervisor", 50_000, true)]
    [InlineData("supervisor", 150_000, false)]
    [InlineData("manager", 150_000, true)]
    public void CheckRejection_UsesApprovalAuthority(string role, int amount, bool allowed)
    {
        var pending = PendingTransaction(Claim(), ReserveComponentType.Indemnity, amount);

        Assert.Equal(allowed, ReserveRules.CheckRejection(pending, role).Count == 0);
    }

    [Fact]
    public void CheckRejection_NonPendingTransaction_IsRejected()
    {
        var approved = ApprovedReserve(Claim(), ReserveComponentType.Indemnity, 5000).History.Single();

        Assert.Equal(["ApprovalStatus"], Fields(ReserveRules.CheckRejection(approved, "manager")));
    }

    [Fact]
    public void CheckRetraction_OnlySubmitterMayRetract()
    {
        var pending = PendingTransaction(Claim(), ReserveComponentType.Indemnity, 50000, submittedBy: HandlerId);

        Assert.Empty(ReserveRules.CheckRetraction(pending, HandlerId));
        Assert.Equal(["SubmittedBy"], Fields(ReserveRules.CheckRetraction(pending, SupervisorId)));
    }

    [Fact]
    public void CheckRetraction_NonPendingTransaction_IsRejected()
    {
        var pending = PendingTransaction(Claim(), ReserveComponentType.Indemnity, 50000);
        pending.Retract();

        Assert.Equal(["ApprovalStatus"], Fields(ReserveRules.CheckRetraction(pending, HandlerId)));
    }

    [Fact]
    public void CheckReserveLimitOverride_ManagerOnlyAndOnce()
    {
        var claim = Claim();

        Assert.Equal(["Role"], Fields(ReserveRules.CheckReserveLimitOverride(claim, "supervisor")));
        Assert.Empty(ReserveRules.CheckReserveLimitOverride(claim, "manager"));

        claim.SetReserveLimitOverride(ManagerId, "Approved", Now);
        Assert.Equal(["ReserveLimitOverride"], Fields(ReserveRules.CheckReserveLimitOverride(claim, "manager")));
    }
}
