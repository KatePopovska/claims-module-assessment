using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RemoveClaimParty;

public class RemoveClaimPartyCommandHandler(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService) : IRequestHandler<RemoveClaimPartyCommand>
{
    public async Task Handle(RemoveClaimPartyCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithPartiesAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var party = claim.Parties.FirstOrDefault(p => p.Id == request.PartyId)
            ?? throw new NotFoundException(nameof(ClaimParty), request.PartyId);

        if (!party.IsActive)
        {
            return;
        }

        if (claim.IsLastActiveClaimant(party))
        {
            throw new ValidationException([new ValidationFailure("Parties", "Cannot remove the last active Claimant party.")]);
        }

        party.IsActive = false;

        auditLogService.Log(claim, AuditEventType.PARTY_REMOVED, $"{party.PartyRole} party {party.GetDisplayName()} removed.", oldValue: $"{party.PartyRole}: {party.GetDisplayName()}", relatedEntityId: party.Id, relatedEntityType: nameof(ClaimParty));

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
