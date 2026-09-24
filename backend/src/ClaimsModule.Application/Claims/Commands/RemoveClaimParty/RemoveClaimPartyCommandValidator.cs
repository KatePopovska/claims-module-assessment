using FluentValidation;

namespace ClaimsModule.Application.Claims.Commands.RemoveClaimParty;

public class RemoveClaimPartyCommandValidator : AbstractValidator<RemoveClaimPartyCommand>
{
    public RemoveClaimPartyCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.PartyId).NotEmpty();
    }
}
