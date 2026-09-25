using Hangfire;

namespace ClaimsModule.Infrastructure.Jobs;

public static class RecurringJobs
{
    public static void Register(IRecurringJobManager recurringJobManager)
    {
        recurringJobManager.AddOrUpdate<SlaMonitoringJob>(SlaMonitoringJob.RecurringJobId, job => job.ExecuteAsync(CancellationToken.None), SlaMonitoringJob.Schedule);
    }
}
