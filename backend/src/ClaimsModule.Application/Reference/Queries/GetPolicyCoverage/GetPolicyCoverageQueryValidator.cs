using FluentValidation;

namespace ClaimsModule.Application.Reference.Queries.GetPolicyCoverage;

public class GetPolicyCoverageQueryValidator : AbstractValidator<GetPolicyCoverageQuery>
{
    public GetPolicyCoverageQueryValidator()
    {
        RuleFor(q => q.PolicyId).NotEmpty();
    }
}
