using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.AdjustReserve;

public record AdjustReserveCommand(Guid ClaimId, Guid ReserveComponentId, decimal Amount, string? ChangeReason) : IRequest<ReserveSubmissionResultDto>;
