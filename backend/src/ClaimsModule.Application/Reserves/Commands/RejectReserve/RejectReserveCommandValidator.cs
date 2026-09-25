using FluentValidation;

namespace ClaimsModule.Application.Reserves.Commands.RejectReserve;

public class RejectReserveCommandValidator : AbstractValidator<RejectReserveCommand>
{
    public RejectReserveCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.TransactionId).NotEmpty();
        RuleFor(c => c.RejectionReason).NotEmpty().WithMessage("A rejection reason is required.").MaximumLength(1000);
    }
}
