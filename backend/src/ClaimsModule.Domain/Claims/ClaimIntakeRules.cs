namespace ClaimsModule.Domain.Claims;

public static class ClaimIntakeRules
{
    public const string PolicyUnknownWarning = "Policy unknown: the claim was created without a linked policy.";
    public const string LossDateOutsidePolicyWarning = "Loss date is outside the policy effective period.";
    public const string NoRiskObjectsWarning = "No risk objects are linked to the claim.";

    public static IReadOnlyList<string> GetWarnings(Claim claim)
    {
        var warnings = new List<string>();

        if (claim.PolicyId is null)
        {
            warnings.Add(PolicyUnknownWarning);
        }
        else if (IsLossDateOutsidePolicyPeriod(claim))
        {
            warnings.Add(LossDateOutsidePolicyWarning);
        }

        if (claim.RiskObjects.Count == 0)
        {
            warnings.Add(NoRiskObjectsWarning);
        }

        return warnings;
    }

    public static bool IsLossDateOutsidePolicyPeriod(Claim claim) =>
        claim.Policy is not null && claim.LossEvent is not null
        && (claim.LossEvent.LossDate < claim.Policy.EffectiveDate || claim.LossEvent.LossDate > claim.Policy.ExpirationDate);
}
