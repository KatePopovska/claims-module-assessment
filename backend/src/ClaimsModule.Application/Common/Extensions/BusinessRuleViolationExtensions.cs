using ClaimsModule.Domain.Common;
using FluentValidation;
using FluentValidation.Results;

namespace ClaimsModule.Application.Common.Extensions;

public static class BusinessRuleViolationExtensions
{
    public static void ThrowIfAny(this IReadOnlyCollection<BusinessRuleViolation> violations)
    {
        if (violations.Count > 0)
        {
            throw new ValidationException(violations.Select(v => new ValidationFailure(v.Field, v.Message)));
        }
    }
}
