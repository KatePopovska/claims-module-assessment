using ClaimsModule.Application.Claims.Commands.DetectSlaBreaches;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure.Jobs;

public class SlaMonitoringJob(ISender sender, ILogger<SlaMonitoringJob> logger)
{
    public const string RecurringJobId = "sla-monitoring";
    public const string Schedule = "*/15 * * * *";

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var breaches = await sender.Send(new DetectSlaBreachesCommand(), cancellationToken);

        logger.LogInformation("SLA monitoring recorded {BreachCount} breach(es).", breaches);
    }
}
