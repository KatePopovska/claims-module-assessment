using ClaimsModule.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Idempotency;

public class IdempotencyStore(ClaimsDbContext context, ICurrentUserService currentUserService, TimeProvider timeProvider) : IIdempotencyStore
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public Task<IdempotencyEntry?> FindAsync(string key, CancellationToken cancellationToken) =>
        context.Set<IdempotencyRecord>()
            .AsNoTracking()
            .Where(r => r.Key == key)
            .Select(r => new IdempotencyEntry(r.RequestHash, r.StatusCode, r.ContentType, r.Location, r.ResponseBody, r.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryBeginAsync(string key, string requestHash, CancellationToken cancellationToken)
    {
        var organisationId = currentUserService.OrganisationId;
        var userId = currentUserService.UserId;
        var now = timeProvider.GetUtcNow();

        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO dbo.IdempotencyRecords ([Key], RequestHash, OrganisationId, CreatedAt, UserCreated) VALUES ({key}, {requestHash}, {organisationId}, {now}, {userId})",
                cancellationToken);

            return true;
        }
        catch (SqlException ex) when (ex.Number is UniqueIndexViolation or UniqueConstraintViolation)
        {
            return false;
        }
    }

    public Task CompleteAsync(string key, int statusCode, string? contentType, string? location, string body, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var userId = currentUserService.UserId;

        return context.Set<IdempotencyRecord>()
            .Where(r => r.Key == key)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.StatusCode, statusCode)
                .SetProperty(r => r.ContentType, contentType)
                .SetProperty(r => r.Location, location)
                .SetProperty(r => r.ResponseBody, body)
                .SetProperty(r => r.UpdatedAt, now)
                .SetProperty(r => r.UserModified, userId),
                cancellationToken);
    }

    public Task ReleaseAsync(string key, CancellationToken cancellationToken) =>
        context.Set<IdempotencyRecord>()
            .Where(r => r.Key == key)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken) =>
        context.Set<IdempotencyRecord>()
            .IgnoreQueryFilters()
            .Where(r => r.CreatedAt < olderThan)
            .ExecuteDeleteAsync(cancellationToken);
}
