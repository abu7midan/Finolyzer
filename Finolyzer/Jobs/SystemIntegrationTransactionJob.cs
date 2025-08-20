using Finolyzer.Entities;
using Hangfire;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers.Hangfire;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;

namespace Finolyzer.Jobs;



public class SystemIntegrationTransactionJob
{
    private readonly ILogger<SystemIntegrationTransactionJob> _logger;
    private readonly IRepository<ApplicationSystem, int> _applicationSystemRepository;
    private readonly IRepository<ApplicationIntegrationKey, int> _applicationIntegrationKey;

    public SystemIntegrationTransactionJob(ILogger<SystemIntegrationTransactionJob> logger, IRepository<ApplicationSystem, int> applicationSystemRepository, IRepository<ApplicationIntegrationKey, int> applicationIntegrationKey)
    {
        _logger = logger;
        _applicationSystemRepository = applicationSystemRepository;
        _applicationIntegrationKey = applicationIntegrationKey;
    }

    public async Task RunAsync(ApigeeAnalyticsJobArgs args)
    {
        _logger.LogInformation("Running CostSummaryJob for service: {ServiceName} at {Time}", args.ServiceName, DateTime.Now);


        var apps = await _applicationSystemRepository.GetListAsync();
        foreach (var app in apps)
        {
            Console.WriteLine(app.Name);
        }
        Console.WriteLine(
            $"[Apigee Job] Service={args.ServiceName}, ReportDate={args.ReportDate}"
        );
        _logger.LogInformation("Completed CostSummaryJob for service: {ServiceName}", args.ServiceName);
    }
}
