using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Reserves;
using Hangfire;

namespace ClaimsModule.Infrastructure.Jobs;

public class HangfireGlPostingScheduler(IBackgroundJobClient backgroundJobClient) : IGlPostingScheduler
{
    public void Enqueue(ReserveHistory transaction)
    {
        var reserveHistoryId = transaction.Id;
        var claimId = transaction.ClaimId;
        var idempotencyKey = transaction.IdempotencyKey;

        backgroundJobClient.Enqueue<PostGLReserveChangeJob>(job => job.ExecuteAsync(reserveHistoryId, claimId, idempotencyKey, null, CancellationToken.None));
    }
}
