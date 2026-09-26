using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClaimsModule.Application.Common.Interfaces;
using FluentValidation;
using FluentValidation.Results;

namespace ClaimsModule.API.Middleware;

public class IdempotencyMiddleware(RequestDelegate next, TimeProvider timeProvider)
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotency-Replayed";
    public const int KeyMaxLength = 200;
    public static readonly TimeSpan AbandonedAfter = TimeSpan.FromMinutes(2);

    private static readonly string[] WriteMethods = [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        if (!WriteMethods.Contains(context.Request.Method) || !context.Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            await next(context);
            return;
        }

        var key = headerValues.ToString().Trim();

        if (key.Length is 0 or > KeyMaxLength)
        {
            throw new ValidationException([new ValidationFailure(HeaderName, $"{HeaderName} must be between 1 and {KeyMaxLength} characters.")]);
        }

        var requestHash = await ComputeRequestHashAsync(context);
        var cancellationToken = context.RequestAborted;

        if (!await store.TryBeginAsync(key, requestHash, cancellationToken))
        {
            var existing = await store.FindAsync(key, cancellationToken);

            if (existing is null || IsAbandoned(existing))
            {
                await store.ReleaseAsync(key, cancellationToken);

                if (!await store.TryBeginAsync(key, requestHash, cancellationToken))
                {
                    await WriteInProgressAsync(context);
                    return;
                }
            }
            else
            {
                await RespondToExistingAsync(context, existing, requestHash);
                return;
            }
        }

        await ExecuteAndStoreAsync(context, store, key);
    }

    private bool IsAbandoned(IdempotencyEntry entry) =>
        entry.StatusCode is null && timeProvider.GetUtcNow() - entry.CreatedAt > AbandonedAfter;

    private async Task ExecuteAndStoreAsync(HttpContext context, IIdempotencyStore store, string key)
    {
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        var completed = false;

        try
        {
            await next(context);

            buffer.Position = 0;
            var body = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync(context.RequestAborted);

            if (context.Response.StatusCode is >= 200 and < 300)
            {
                await store.CompleteAsync(key, context.Response.StatusCode, context.Response.ContentType, context.Response.Headers.Location.ToString() is { Length: > 0 } location ? location : null, body, CancellationToken.None);
                completed = true;
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBody;

            if (!completed)
            {
                await store.ReleaseAsync(key, CancellationToken.None);
            }
        }
    }

    private static async Task RespondToExistingAsync(HttpContext context, IdempotencyEntry existing, string requestHash)
    {
        if (existing.RequestHash != requestHash)
        {
            throw new ValidationException([new ValidationFailure(HeaderName, $"This {HeaderName} was already used for a different request.")]);
        }

        if (existing.StatusCode is null)
        {
            await WriteInProgressAsync(context);
            return;
        }

        context.Response.StatusCode = existing.StatusCode.Value;
        context.Response.Headers[ReplayedHeaderName] = "true";

        if (existing.ContentType is not null)
        {
            context.Response.ContentType = existing.ContentType;
        }

        if (existing.Location is not null)
        {
            context.Response.Headers.Location = existing.Location;
        }

        if (!string.IsNullOrEmpty(existing.Body))
        {
            await context.Response.WriteAsync(existing.Body, context.RequestAborted);
        }
    }

    private static async Task WriteInProgressAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.Conflict;

        var body = new
        {
            type = "Conflict",
            title = $"A request with this {HeaderName} is still being processed.",
            status = 409
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body), context.RequestAborted);
    }

    private static async Task<string> ComputeRequestHashAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        hash.AppendData(Encoding.UTF8.GetBytes($"{context.Request.Method}\n{context.Request.Path}{context.Request.QueryString}\n{userId}\n"));

        var chunk = new byte[81920];
        int read;
        while ((read = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
        {
            hash.AppendData(chunk, 0, read);
        }

        context.Request.Body.Position = 0;

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
