using ClaimsModule.Application.Common.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure.Jobs;

public class IdempotencyCleanupJob(IIdempotencyStore store, TimeProvider timeProvider, ILogger<IdempotencyCleanupJob> logger)
{
    public const string RecurringJobId = "idempotency-cleanup";
    public const string Schedule = "0 * * * *";
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(24);

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deleted = await store.DeleteExpiredAsync(timeProvider.GetUtcNow() - RetentionPeriod, cancellationToken);

        logger.LogInformation("Idempotency cleanup removed {DeletedCount} expired record(s).", deleted);
    }
}
