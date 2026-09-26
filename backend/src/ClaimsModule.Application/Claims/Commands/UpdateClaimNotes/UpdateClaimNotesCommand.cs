using MediatR;

namespace ClaimsModule.Application.Claims.Commands.UpdateClaimNotes;

public record UpdateClaimNotesCommand(Guid ClaimId, string? Notes) : IRequest;
