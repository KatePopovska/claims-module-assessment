using System.Text;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Common.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace ClaimsModule.API.UnitTests.Middleware;

public class IdempotencyMiddlewareTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryIdempotencyStore _store = new();
    private int _executions;

    private IdempotencyMiddleware Middleware(int statusCode = StatusCodes.Status201Created, string responseBody = "{\"id\":1}", Exception? throws = null) =>
        new(async context =>
        {
            _executions++;
            if (throws is not null)
            {
                throw throws;
            }

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.Headers.Location = "api/claims/1";
            await context.Response.WriteAsync(responseBody);
        }, new FixedTimeProvider(Now));

    private static DefaultHttpContext Request(string method = "POST", string? key = "key-1", string body = "{\"a\":1}")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/api/claims";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Response.Body = new MemoryStream();

        if (key is not null)
        {
            context.Request.Headers[IdempotencyMiddleware.HeaderName] = key;
        }

        return context;
    }

    private static string ResponseBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }

    [Fact]
    public async Task WithoutHeader_PassesThroughAndStoresNothing()
    {
        await Middleware().InvokeAsync(Request(key: null), _store);

        Assert.Equal(1, _executions);
        Assert.Empty(_store.Entries);
    }

    [Fact]
    public async Task ReadRequest_IsIgnored()
    {
        await Middleware().InvokeAsync(Request(method: "GET"), _store);

        Assert.Equal(1, _executions);
        Assert.Empty(_store.Entries);
    }

    [Fact]
    public async Task FirstSuccessfulRequest_IsExecutedAndStored()
    {
        var context = Request();

        await Middleware().InvokeAsync(context, _store);

        Assert.Equal("{\"id\":1}", ResponseBody(context));
        var stored = _store.Entries["key-1"];
        Assert.Equal((201, "application/json", "api/claims/1", "{\"id\":1}"), (stored.StatusCode, stored.ContentType, stored.Location, stored.Body));
    }

    [Fact]
    public async Task RepeatedRequest_ReplaysStoredResponseWithoutExecuting()
    {
        await Middleware().InvokeAsync(Request(), _store);
        var replay = Request();

        await Middleware().InvokeAsync(replay, _store);

        Assert.Equal(1, _executions);
        Assert.Equal(201, replay.Response.StatusCode);
        Assert.Equal("true", replay.Response.Headers[IdempotencyMiddleware.ReplayedHeaderName]);
        Assert.Equal("api/claims/1", replay.Response.Headers.Location);
        Assert.Equal("{\"id\":1}", ResponseBody(replay));
    }

    [Fact]
    public async Task SameKeyWithDifferentBody_IsRejected()
    {
        await Middleware().InvokeAsync(Request(), _store);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => Middleware().InvokeAsync(Request(body: "{\"a\":2}"), _store));

        Assert.Contains(exception.Errors, e => e.PropertyName == IdempotencyMiddleware.HeaderName);
        Assert.Equal(1, _executions);
    }

    [Fact]
    public async Task KeyStillInProgress_Returns409()
    {
        var context = Request();
        await _store.TryBeginAsync("key-1", await HashOf(Request()), CancellationToken.None);
        _store.Entries["key-1"] = _store.Entries["key-1"] with { CreatedAt = Now };

        await Middleware().InvokeAsync(context, _store);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal(0, _executions);
    }

    [Fact]
    public async Task AbandonedInProgressRecord_IsTakenOver()
    {
        await _store.TryBeginAsync("key-1", "stale", CancellationToken.None);
        _store.Entries["key-1"] = _store.Entries["key-1"] with { CreatedAt = Now - IdempotencyMiddleware.AbandonedAfter - TimeSpan.FromSeconds(1) };

        await Middleware().InvokeAsync(Request(), _store);

        Assert.Equal(1, _executions);
        Assert.Equal(201, _store.Entries["key-1"].StatusCode);
    }

    [Fact]
    public async Task UnsuccessfulResponse_ReleasesKeyAndIsReturned()
    {
        var context = Request();

        await Middleware(statusCode: StatusCodes.Status404NotFound, responseBody: "{\"type\":\"NotFound\"}").InvokeAsync(context, _store);

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal("{\"type\":\"NotFound\"}", ResponseBody(context));
        Assert.Empty(_store.Entries);
    }

    [Fact]
    public async Task Exception_ReleasesKeyRestoresBodyAndRethrows()
    {
        var context = Request();
        var originalBody = context.Response.Body;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Middleware(throws: new InvalidOperationException()).InvokeAsync(context, _store));

        Assert.Same(originalBody, context.Response.Body);
        Assert.Empty(_store.Entries);
    }

    [Fact]
    public async Task TooLongKey_IsRejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Middleware().InvokeAsync(Request(key: new string('k', IdempotencyMiddleware.KeyMaxLength + 1)), _store));

        Assert.Equal(0, _executions);
    }

    private async Task<string> HashOf(HttpContext context)
    {
        var probe = new InMemoryIdempotencyStore();
        await new IdempotencyMiddleware(_ => Task.CompletedTask, new FixedTimeProvider(Now)).InvokeAsync(context, probe);
        return probe.LastBeginHash!;
    }

    private sealed class InMemoryIdempotencyStore : IIdempotencyStore
    {
        public Dictionary<string, IdempotencyEntry> Entries { get; } = [];
        public string? LastBeginHash { get; private set; }

        public Task<IdempotencyEntry?> FindAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(Entries.GetValueOrDefault(key));

        public Task<bool> TryBeginAsync(string key, string requestHash, CancellationToken cancellationToken)
        {
            LastBeginHash = requestHash;
            return Task.FromResult(Entries.TryAdd(key, new IdempotencyEntry(requestHash, null, null, null, null, Now)));
        }

        public Task CompleteAsync(string key, int statusCode, string? contentType, string? location, string body, CancellationToken cancellationToken)
        {
            Entries[key] = Entries[key] with { StatusCode = statusCode, ContentType = contentType, Location = location, Body = body };
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string key, CancellationToken cancellationToken)
        {
            Entries.Remove(key);
            return Task.CompletedTask;
        }

        public Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
