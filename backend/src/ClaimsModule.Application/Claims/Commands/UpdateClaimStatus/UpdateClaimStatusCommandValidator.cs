using FluentValidation;

namespace ClaimsModule.Application.Claims.Commands.UpdateClaimStatus;

public class UpdateClaimStatusCommandValidator : AbstractValidator<UpdateClaimStatusCommand>
{
    public UpdateClaimStatusCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.TargetStatus).IsInEnum();
        RuleFor(c => c.Reason).MaximumLength(500);
    }
}
