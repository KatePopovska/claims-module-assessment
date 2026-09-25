using ClaimsModule.Application.Reserves.Commands.MarkGlPostingFailed;
using ClaimsModule.Application.Reserves.Commands.PostGlReserveChange;
using Hangfire;
using Hangfire.Server;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Infrastructure.Jobs;

public class PostGLReserveChangeJob(ISender sender, IServiceScopeFactory scopeFactory)
{
    public const int MaxRetryAttempts = 10;

    [AutomaticRetry(Attempts = MaxRetryAttempts)]
    public async Task ExecuteAsync(Guid reserveHistoryId, Guid claimId, string idempotencyKey, PerformContext? context, CancellationToken cancellationToken)
    {
        try
        {
            await sender.Send(new PostGlReserveChangeCommand(reserveHistoryId, claimId, idempotencyKey, context?.BackgroundJob.Id), cancellationToken);
        }
        catch (Exception ex) when (IsFinalAttempt(context))
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new MarkGlPostingFailedCommand(reserveHistoryId, claimId, ex.Message), cancellationToken);
            throw;
        }
    }

    private static bool IsFinalAttempt(PerformContext? context) =>
        context is not null && context.GetJobParameter<int>("RetryCount") >= MaxRetryAttempts;
}
