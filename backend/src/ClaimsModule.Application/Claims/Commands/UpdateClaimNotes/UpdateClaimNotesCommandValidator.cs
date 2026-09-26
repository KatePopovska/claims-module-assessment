using FluentValidation;

namespace ClaimsModule.Application.Claims.Commands.UpdateClaimNotes;

public class UpdateClaimNotesCommandValidator : AbstractValidator<UpdateClaimNotesCommand>
{
    public UpdateClaimNotesCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(4000);
    }
}
