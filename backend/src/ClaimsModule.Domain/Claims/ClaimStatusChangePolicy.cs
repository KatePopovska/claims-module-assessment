using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Domain.Claims;

public static class ClaimStatusChangePolicy
{
    private const int MinimumLossDescriptionLength = 20;

    public static StatusChangeEvaluation Evaluate(Claim claim, StatusChangeRequest request, DateTimeOffset now, bool isCauseOfLossCodeActive)
    {
        var from = claim.Status;
        var to = request.TargetStatus;

        if (!ClaimStatusTransitions.IsValid(from, to))
        {
            var validNext = ClaimStatusTransitions.GetValidNextStatuses(from);
            var validNextText = validNext.Count == 0 ? "none" : string.Join(", ", validNext);
            return new StatusChangeEvaluation(
                [new StatusChangeIssue("Status", $"Transition from {from} to {to} is not permitted. Valid next statuses: {validNextText}.")],
                []);
        }

        var issues = new List<StatusChangeIssue>();
        var acknowledgedWarnings = new List<string>();

        if (!ClaimStatusTransitions.IsPermittedFor(from, to, request.ActorRole))
        {
            issues.Add(new StatusChangeIssue("Role", $"Your role does not permit moving a claim from {from} to {to}."));
        }

        switch (to)
        {
            case ClaimStatus.Open when from != ClaimStatus.Reopened:
                AddCriticalIssues(claim, now, isCauseOfLossCodeActive, issues);
                AddMissingClaimantIssue(claim, issues);
                if (from == ClaimStatus.Draft)
                {
                    CheckWarnings(claim, request.WarningsAcknowledged, issues, acknowledgedWarnings);
                }
                break;

            case ClaimStatus.PendingPayment:
                if (!claim.ReserveComponents.Any(rc => rc.Status == ReserveComponentStatus.Active && GetApprovedBalance(rc) > 0))
                {
                    issues.Add(new StatusChangeIssue("Reserves", "At least one reserve component with a current approved balance is required before moving to PendingPayment."));
                }
                break;

            case ClaimStatus.Closed:
                if (claim.ReserveComponents.SelectMany(rc => rc.History).Any(h => h.ApprovalStatus == ReserveApprovalStatus.PendingApproval))
                {
                    issues.Add(new StatusChangeIssue("Reserves", "No reserve transaction may be PendingApproval when closing a claim."));
                }
                AddCriticalIssues(claim, now, isCauseOfLossCodeActive, issues);
                AddMissingClaimantIssue(claim, issues);
                if (claim.ReserveComponents.Any(rc => GetApprovedBalance(rc) > 0) && string.IsNullOrWhiteSpace(request.Reason))
                {
                    issues.Add(new StatusChangeIssue("Reason", "A justification note is required to close a claim with open (non-zero) reserves."));
                }
                break;

            case ClaimStatus.Withdrawn:
                AddMissingReasonIssue(request.Reason, "A withdrawal reason is required.", issues);
                break;

            case ClaimStatus.Reopened:
                AddMissingReasonIssue(request.Reason, "A reopen reason is required.", issues);
                break;
        }

        return new StatusChangeEvaluation(issues, acknowledgedWarnings);
    }

    private static void AddCriticalIssues(Claim claim, DateTimeOffset now, bool isCauseOfLossCodeActive, List<StatusChangeIssue> issues)
    {
        if (claim.LossEvent is null)
        {
            issues.Add(new StatusChangeIssue("LossEvent", "A loss event is required."));
            return;
        }

        if (claim.LossEvent.LossDate > now)
        {
            issues.Add(new StatusChangeIssue("LossDate", "Loss date cannot be in the future."));
        }

        if (string.IsNullOrWhiteSpace(claim.LossEvent.LossDescription) || claim.LossEvent.LossDescription.Length < MinimumLossDescriptionLength)
        {
            issues.Add(new StatusChangeIssue("LossDescription", "Loss description must be at least 20 characters."));
        }

        if (!isCauseOfLossCodeActive)
        {
            issues.Add(new StatusChangeIssue("CauseOfLossCode", "Cause of loss code must exist and be active."));
        }
    }

    private static void AddMissingClaimantIssue(Claim claim, List<StatusChangeIssue> issues)
    {
        if (!claim.Parties.Any(p => p.PartyRole == PartyRole.Claimant && p.IsActive))
        {
            issues.Add(new StatusChangeIssue("Parties", "At least one active Claimant party is required."));
        }
    }

    private static void AddMissingReasonIssue(string? reason, string message, List<StatusChangeIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            issues.Add(new StatusChangeIssue("Reason", message));
        }
    }

    private static void CheckWarnings(Claim claim, bool warningsAcknowledged, List<StatusChangeIssue> issues, List<string> acknowledgedWarnings)
    {
        foreach (var warning in GetWarningsRequiringAcknowledgement(claim))
        {
            if (warningsAcknowledged)
            {
                acknowledgedWarnings.Add(warning);
            }
            else
            {
                issues.Add(new StatusChangeIssue("Warnings", $"{warning} Acknowledge this warning to open the claim."));
            }
        }
    }

    private static IEnumerable<string> GetWarningsRequiringAcknowledgement(Claim claim)
    {
        if (claim.Policy is not null && claim.LossEvent is not null
            && (claim.LossEvent.LossDate < claim.Policy.EffectiveDate || claim.LossEvent.LossDate > claim.Policy.ExpirationDate))
        {
            yield return "Loss date is outside the policy effective period.";
        }
    }

    private static decimal GetApprovedBalance(ClaimReserveComponent component) =>
        component.History
            .Where(h => h.ApprovalStatus is ReserveApprovalStatus.Approved or ReserveApprovalStatus.AutoApproved)
            .Sum(h => h.Amount);
}
