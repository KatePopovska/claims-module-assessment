using FluentValidation;

namespace ClaimsModule.Application.Claims.Queries.ListClaims;

public class ListClaimsQueryValidator : AbstractValidator<ListClaimsQuery>
{
    public ListClaimsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
        RuleForEach(q => q.Status).IsInEnum();
        RuleFor(q => q.DateTo)
            .GreaterThanOrEqualTo(q => q.DateFrom)
            .When(q => q.DateFrom.HasValue && q.DateTo.HasValue)
            .WithMessage("DateTo must be on or after DateFrom.");
    }
}
