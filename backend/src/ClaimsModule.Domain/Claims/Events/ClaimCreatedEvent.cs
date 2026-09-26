using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Claims.Events;

public record ClaimCreatedEvent(Claim Claim, DateTimeOffset OccurredAt) : IDomainEvent;
