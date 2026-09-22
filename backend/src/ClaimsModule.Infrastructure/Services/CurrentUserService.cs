using System.Security.Claims;
using ClaimsModule.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ClaimsModule.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Role => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);

    public Guid OrganisationId => Guid.Parse(configuration["OrganisationId"]
        ?? throw new InvalidOperationException("OrganisationId is not configured."));
}
