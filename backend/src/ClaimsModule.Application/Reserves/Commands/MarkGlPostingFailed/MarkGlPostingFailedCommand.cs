using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.MarkGlPostingFailed;

public record MarkGlPostingFailedCommand(Guid ReserveHistoryId, Guid ClaimId, string FailureReason) : IRequest;
