using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Claims;

public sealed record StatusChangeRequest(ClaimStatus TargetStatus, string? Reason, string? ActorRole, bool WarningsAcknowledged);

public sealed record StatusChangeIssue(string Field, string Message);

public sealed record StatusChangeEvaluation(IReadOnlyList<StatusChangeIssue> BlockingIssues, IReadOnlyList<string> AcknowledgedWarnings);

public sealed record StatusChange(ClaimStatus From, ClaimStatus To, bool IsAutomatic);
