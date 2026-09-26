using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Claims.Events;

public record ClaimStatusChangedEvent(Claim Claim, ClaimStatus From, ClaimStatus To, bool IsAutomatic, string? Reason, IReadOnlyList<string> AcknowledgedWarnings, DateTimeOffset OccurredAt) : IDomainEvent;
