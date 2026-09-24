using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.UpdateClaimStatus;

public record UpdateClaimStatusCommand(Guid ClaimId, ClaimStatus TargetStatus, string? Reason, bool AcknowledgeWarnings = false) : IRequest<UpdateClaimStatusResult>;

public record UpdateClaimStatusResult(Guid Id, ClaimStatus Status);
