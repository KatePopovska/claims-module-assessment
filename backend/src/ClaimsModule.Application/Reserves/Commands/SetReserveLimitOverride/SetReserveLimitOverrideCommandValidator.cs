using FluentValidation;

namespace ClaimsModule.Application.Reserves.Commands.SetReserveLimitOverride;

public class SetReserveLimitOverrideCommandValidator : AbstractValidator<SetReserveLimitOverrideCommand>
{
    public SetReserveLimitOverrideCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().WithMessage("An override reason is required.").MaximumLength(500);
    }
}
