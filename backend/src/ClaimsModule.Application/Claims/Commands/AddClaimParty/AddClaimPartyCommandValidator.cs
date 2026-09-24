using ClaimsModule.Domain.Enums;
using FluentValidation;

namespace ClaimsModule.Application.Claims.Commands.AddClaimParty;

public class AddClaimPartyCommandValidator : AbstractValidator<AddClaimPartyCommand>
{
    public AddClaimPartyCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.PartyRole).IsInEnum();
        RuleFor(c => c.PartyType).IsInEnum();

        When(c => c.PartyType == PartyType.Person, () =>
        {
            RuleFor(c => c.FirstName).NotEmpty().WithMessage("First name is required for a person party.");
            RuleFor(c => c.LastName).NotEmpty().WithMessage("Last name is required for a person party.");
        });

        When(c => c.PartyType == PartyType.Company, () =>
        {
            RuleFor(c => c.CompanyName).NotEmpty().WithMessage("Company name is required for a company party.");
        });

        RuleFor(c => c.FirstName).MaximumLength(255);
        RuleFor(c => c.LastName).MaximumLength(255);
        RuleFor(c => c.CompanyName).MaximumLength(255);
        RuleFor(c => c.Email).MaximumLength(255);
        RuleFor(c => c.Phone).MaximumLength(50);
    }
}
