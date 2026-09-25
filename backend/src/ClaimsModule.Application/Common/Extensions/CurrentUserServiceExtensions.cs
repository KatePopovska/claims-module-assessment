using ClaimsModule.Application.Common.Interfaces;

namespace ClaimsModule.Application.Common.Extensions;

public static class CurrentUserServiceExtensions
{
    public static Guid GetRequiredUserId(this ICurrentUserService currentUserService) =>
        currentUserService.UserId ?? throw new InvalidOperationException("The current request has no authenticated user id.");
}
