using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.SetReserveLimitOverride;

public record SetReserveLimitOverrideCommand(Guid ClaimId, string Reason) : IRequest;
