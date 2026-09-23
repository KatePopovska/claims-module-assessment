using ClaimsModule.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ClaimsModule.Infrastructure.Services;

public class CorrelationIdProvider(IHttpContextAccessor httpContextAccessor) : ICorrelationIdProvider
{
    public string? CorrelationId => httpContextAccessor.HttpContext?.TraceIdentifier;
}
