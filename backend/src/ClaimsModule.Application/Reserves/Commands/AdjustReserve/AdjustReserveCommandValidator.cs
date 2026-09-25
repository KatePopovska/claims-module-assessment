using FluentValidation;

namespace ClaimsModule.Application.Reserves.Commands.AdjustReserve;

public class AdjustReserveCommandValidator : AbstractValidator<AdjustReserveCommand>
{
    public AdjustReserveCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.ReserveComponentId).NotEmpty();
        RuleFor(c => c.Amount).PrecisionScale(19, 4, true);
        RuleFor(c => c.ChangeReason).MaximumLength(1000);
    }
}
