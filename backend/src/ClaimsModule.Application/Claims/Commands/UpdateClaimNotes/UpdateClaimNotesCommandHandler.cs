using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.UpdateClaimNotes;

public class UpdateClaimNotesCommandHandler(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService) : IRequestHandler<UpdateClaimNotesCommand>
{
    public async Task Handle(UpdateClaimNotesCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        if (notes == claim.Notes)
        {
            return;
        }

        var previousNotes = claim.Notes;
        claim.Notes = notes;

        auditLogService.Log(claim, AuditEventType.CLAIM_NOTES_UPDATED, "Claim notes updated.", oldValue: previousNotes, newValue: notes);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
