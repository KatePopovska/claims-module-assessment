using FluentValidation;

namespace ClaimsModule.Application.Reserves.Commands.CreateReserve;

public class CreateReserveCommandValidator : AbstractValidator<CreateReserveCommand>
{
    public CreateReserveCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.Component).IsInEnum().WithMessage("Invalid reserve component type.");
        RuleFor(c => c.Amount).PrecisionScale(19, 4, true);
        RuleFor(c => c.ChangeReason).MaximumLength(1000);
    }
}
