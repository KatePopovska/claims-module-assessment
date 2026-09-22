using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimsModule.API.Auth;

public class MockAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "MockBearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var value = authHeader.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = value["Bearer ".Length..].Trim();

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var payload = JsonSerializer.Deserialize<MockTokenPayload>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload is null || string.IsNullOrWhiteSpace(payload.UserId) || string.IsNullOrWhiteSpace(payload.Role))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid mock token payload."));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, payload.UserId),
                new Claim(ClaimTypes.Role, payload.Role)
            };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed mock bearer token."));
        }
    }

    private sealed class MockTokenPayload
    {
        public string? UserId { get; set; }
        public string? Role { get; set; }
    }
}
