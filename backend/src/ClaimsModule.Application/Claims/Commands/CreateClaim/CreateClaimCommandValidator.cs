using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands.CreateClaim;

public class CreateClaimCommandValidator : AbstractValidator<CreateClaimCommand>
{
    public CreateClaimCommandValidator(IApplicationDbContext context, TimeProvider timeProvider)
    {
        RuleFor(c => c.LossDate)
            .LessThanOrEqualTo(_ => timeProvider.GetUtcNow())
            .WithMessage("Loss date cannot be in the future.");

        RuleFor(c => c.LossDescription)
            .NotEmpty().WithMessage("Loss description is required.")
            .MinimumLength(20).WithMessage("Loss description must be at least 20 characters.");

        RuleFor(c => c.CauseOfLossCode)
            .NotEmpty().WithMessage("Cause of loss code is required.")
            .MustAsync((code, cancellationToken) =>
                context.CauseOfLossCodes.AnyAsync(c => c.Code == code && c.IsActive, cancellationToken))
            .WithMessage("Cause of loss code must exist and be active.");

        When(c => c.PolicyId.HasValue, () =>
        {
            RuleFor(c => c.PolicyId!.Value)
                .MustAsync((policyId, cancellationToken) => context.Policies.AnyAsync(p => p.Id == policyId, cancellationToken))
                .WithMessage("Policy not found.");
        });

        RuleForEach(c => c.Parties).ChildRules(party =>
        {
            party.RuleFor(p => p.PartyRole).IsInEnum();
            party.RuleFor(p => p.PartyType).IsInEnum();

            party.When(p => p.PartyType == PartyType.Person, () =>
            {
                party.RuleFor(p => p.FirstName).NotEmpty().WithMessage("First name is required for a person party.");
                party.RuleFor(p => p.LastName).NotEmpty().WithMessage("Last name is required for a person party.");
            });

            party.When(p => p.PartyType == PartyType.Company, () =>
            {
                party.RuleFor(p => p.CompanyName).NotEmpty().WithMessage("Company name is required for a company party.");
            });
        });

        RuleForEach(c => c.RiskObjects).ChildRules(riskObject =>
        {
            riskObject.RuleFor(r => r.AssetType).IsInEnum();
            riskObject.RuleFor(r => r.AssetDescription).NotEmpty().WithMessage("Asset description is required.");
        });

        When(c => c.InitialReserve is not null, () =>
        {
            RuleFor(c => c.PolicyId).NotNull().WithMessage("No policy linked. Policy must be associated before reserves can be set.");
            RuleFor(c => c.InitialReserve!.Component).IsInEnum().WithMessage("Invalid reserve component type.");
            RuleFor(c => c.InitialReserve!.Amount).PrecisionScale(19, 4, true);
            RuleFor(c => c.InitialReserve!.ChangeReason).MaximumLength(1000);
        });
    }
}
