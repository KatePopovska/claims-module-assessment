using FluentValidation;

namespace ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;

public class GetClaimAuditLogQueryValidator : AbstractValidator<GetClaimAuditLogQuery>
{
    public GetClaimAuditLogQueryValidator()
    {
        RuleFor(q => q.ClaimId).NotEmpty();
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
