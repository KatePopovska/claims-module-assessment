using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Reserves;

public static class ReserveRules
{
    public const string ReserveLimitWarning = "Total reserves will exceed $10,000,000. Manager override required.";

    public static IReadOnlyList<BusinessRuleViolation> CheckNewReserve(Claim claim, ReserveComponentType component, decimal amount)
    {
        var violations = new List<BusinessRuleViolation>();

        AddMissingPolicyViolation(claim, violations);

        if (claim.ReserveComponents.Any(rc => rc.Component == component))
        {
            violations.Add(new BusinessRuleViolation("Component", $"A {component} reserve already exists on this claim. Adjust it instead."));
        }

        if (component == ReserveComponentType.SubrogationRecoverable ? amount == 0 : amount <= 0)
        {
            violations.Add(new BusinessRuleViolation("Amount", "Reserve amount must be greater than zero."));
        }

        return violations;
    }

    public static IReadOnlyList<BusinessRuleViolation> CheckAdjustment(Claim claim, ClaimReserveComponent component, decimal amount)
    {
        var violations = new List<BusinessRuleViolation>();

        AddMissingPolicyViolation(claim, violations);

        if (amount == 0)
        {
            violations.Add(new BusinessRuleViolation("Amount", "Adjustment amount must not be zero."));
        }

        if (component.HasPendingTransaction())
        {
            violations.Add(new BusinessRuleViolation("Component", $"The {component.Component} reserve has a pending transaction. Retract it before submitting another change."));
        }

        if (component.Component != ReserveComponentType.SubrogationRecoverable && component.GetApprovedBalance() + amount < 0)
        {
            violations.Add(new BusinessRuleViolation("Amount", $"The {component.Component} reserve balance cannot go below zero."));
        }

        return violations;
    }

    public static IReadOnlyList<string> GetSubmissionWarnings(Claim claim, decimal amount) =>
        claim.WouldExceedReserveLimit(amount) ? [ReserveLimitWarning] : [];

    public static IReadOnlyList<BusinessRuleViolation> CheckApproval(Claim claim, ReserveHistory transaction, string? role, Guid? userId)
    {
        if (!transaction.IsPending())
        {
            return [NotPendingViolation(transaction, "approved")];
        }

        var violations = new List<BusinessRuleViolation>();

        if (!ReserveAuthority.CanApprove(role, transaction.Amount))
        {
            violations.Add(new BusinessRuleViolation("Role", "Your role does not have authority to approve this reserve amount."));
        }

        if (transaction.SubmittedByUserId == userId)
        {
            violations.Add(new BusinessRuleViolation("Approver", "Self-approval is not permitted."));
        }

        if (claim.WouldExceedReserveLimit(transaction.Amount))
        {
            violations.Add(new BusinessRuleViolation("Amount", ReserveLimitWarning));
        }

        return violations;
    }

    public static IReadOnlyList<BusinessRuleViolation> CheckRejection(ReserveHistory transaction, string? role)
    {
        if (!transaction.IsPending())
        {
            return [NotPendingViolation(transaction, "rejected")];
        }

        return ReserveAuthority.CanApprove(role, transaction.Amount)
            ? []
            : [new BusinessRuleViolation("Role", "Your role does not have authority to reject this reserve amount.")];
    }

    public static IReadOnlyList<BusinessRuleViolation> CheckRetraction(ReserveHistory transaction, Guid? userId)
    {
        if (!transaction.IsPending())
        {
            return [NotPendingViolation(transaction, "retracted")];
        }

        return transaction.SubmittedByUserId == userId
            ? []
            : [new BusinessRuleViolation("SubmittedBy", "Only the submitter can retract a pending reserve transaction.")];
    }

    public static IReadOnlyList<BusinessRuleViolation> CheckReserveLimitOverride(Claim claim, string? role)
    {
        var violations = new List<BusinessRuleViolation>();

        if (!ReserveAuthority.IsManager(role))
        {
            violations.Add(new BusinessRuleViolation("Role", "Only a Manager can set the reserve limit override."));
        }

        if (claim.ReserveLimitOverride)
        {
            violations.Add(new BusinessRuleViolation("ReserveLimitOverride", "The reserve limit override is already set on this claim."));
        }

        return violations;
    }

    private static void AddMissingPolicyViolation(Claim claim, List<BusinessRuleViolation> violations)
    {
        if (claim.PolicyId is null)
        {
            violations.Add(new BusinessRuleViolation("PolicyId", "No policy linked. Policy must be associated before reserves can be set."));
        }
    }

    private static BusinessRuleViolation NotPendingViolation(ReserveHistory transaction, string action) =>
        new("ApprovalStatus", $"Only a PendingApproval transaction can be {action}. This transaction is {transaction.ApprovalStatus}.");
}
