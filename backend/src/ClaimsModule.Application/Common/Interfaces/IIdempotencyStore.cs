namespace ClaimsModule.Application.Common.Interfaces;

public interface IIdempotencyStore
{
    Task<IdempotencyEntry?> FindAsync(string key, CancellationToken cancellationToken);
    Task<bool> TryBeginAsync(string key, string requestHash, CancellationToken cancellationToken);
    Task CompleteAsync(string key, int statusCode, string? contentType, string? location, string body, CancellationToken cancellationToken);
    Task ReleaseAsync(string key, CancellationToken cancellationToken);
    Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken);
}

public record IdempotencyEntry(string RequestHash, int? StatusCode, string? ContentType, string? Location, string? Body, DateTimeOffset CreatedAt);
