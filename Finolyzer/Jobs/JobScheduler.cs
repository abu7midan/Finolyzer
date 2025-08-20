using Finolyzer.Entities;
using Hangfire;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Finolyzer.Jobs;

[Dependency(ServiceLifetime.Transient)]
public class JobScheduler
{
    private readonly IRepository<ApplicationIntegrationKey, int> _applicationIntegrationKey;
    private readonly IRecurringJobManager _recurringJobManager;

    public JobScheduler(
        IRepository<ApplicationIntegrationKey, int> applicationIntegrationKey,
        IRecurringJobManager recurringJobManager)
    {
        _applicationIntegrationKey = applicationIntegrationKey;
        _recurringJobManager = recurringJobManager;
    }

    public async Task ScheduleJobsAsync()
    {
        var services = await _applicationIntegrationKey.GetListAsync(true);

        foreach (var service in services)
        {
            var args = new ApigeeAnalyticsJobArgs
            {
                ApplicationName = service.ApplicationSystem?.Name,
                ApplicationKey = service.UserName,
                ServiceName = service.IntegrationService?.SystemMappingKey.ToLower(),
                ReportDate = DateTime.UtcNow
            };

            // Create a unique recurring job id per service
            var jobId = $"apigee-job-{args.ServiceName}-{args.ApplicationName}-{args.ApplicationKey}";

            // Schedule it monthly (1st day of month, 1am)
            _recurringJobManager.AddOrUpdate<SystemIntegrationTransactionJob>(
                jobId,
                job => job.RunAsync(args),
                //"0 1 1 * *" 
                "* * * * *" 
            );
        }
    }
}
