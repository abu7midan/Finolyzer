using Finolyzer.Entities;
using Hangfire;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Finolyzer.Jobs
{
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
            var applicationIntegrationKeys = await _applicationIntegrationKey.GetListAsync(true);

            foreach (var service in applicationIntegrationKeys)
            {
                var args = new ApigeeAnalyticsJobArgs
                {
                    ApplicationSystemId = service.ApplicationSystemId,
                    ApplicationName = service.ApplicationSystem?.Name,
                    ApplicationIntegrationKeyId = service.Id,
                    ApplicationKey = service.UserName,
                    IntegrationServiceId = service.IntegrationServiceId,
                    IntegrationServiceName = service.IntegrationService?.SystemMappingKey,
                    ReportDate = DateTime.Now,
                    TimlyRequestType = TimlyRequestType.Monthly // Can change this as needed (Daily, Monthly, Yearly)
                };

                // Create a unique recurring job id per service
                var jobId = $"apigee-job-{args.IntegrationServiceName}-{args.ApplicationName}-{args.ApplicationKey}";
                string cronExpression;

                // Define cron based on TimlyRequestType
                switch (args.TimlyRequestType)
                {
                    case TimlyRequestType.Monthly:
                        // Last day of the month at midnight (00:00:00) - Example: 59 23 28-31 * * ? (last day of every month)
                        //cronExpression = "0 0 1 * *";  // At 00:00:00 on the 1st day of each month (Can be adjusted for the last day of month)
                        cronExpression = "48 15 14 10 *";  // At 00:00:00 on the 1st day of each month (Can be adjusted for the last day of month)
                        break;

                    case TimlyRequestType.Daily:
                        // Last moment of the day: 23:59:59 (59th minute, 59th second)
                        cronExpression = "59 59 23 * * *";  // Every day at 23:59:59
                        break;

                    case TimlyRequestType.Yearly:
                        // Last day of the year (December 31st, 23:59:59)
                        cronExpression = "59 59 23 31 12 *";  // Every year on December 31st at 23:59:59
                        break;

                    default:
                        // Default case - Daily job if no valid TimlyRequestType is set
                        cronExpression = "59 59 23 * * *";  // Every day at 23:59:59
                        break;
                }

                // Schedule the recurring job with the appropriate cron expression
                _recurringJobManager.AddOrUpdate<SystemIntegrationTransactionJob>(
                    jobId,
                    job => job.RunAsync(args),
                    cronExpression, // Cron expression for the job
                    TimeZoneInfo.Local // Use local timezone (can be changed to a specific timezone like Saudi Time)
                );
            }
        }
    }
}

