using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.PostGlReserveChange;

public record PostGlReserveChangeCommand(Guid ReserveHistoryId, Guid ClaimId, string IdempotencyKey, string? JobId) : IRequest;
