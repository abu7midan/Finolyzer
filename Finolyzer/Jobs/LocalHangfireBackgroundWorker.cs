using Hangfire;
using Volo.Abp.BackgroundWorkers.Hangfire;
using Volo.Abp.Uow;

namespace Finolyzer.Jobs;

public class LocalHangfireBackgroundWorker : HangfireBackgroundWorkerBase
{
    public LocalHangfireBackgroundWorker(IConfiguration configuration)
    {
        RecurringJobId = nameof(LocalHangfireBackgroundWorker);
        CronExpression = configuration.GetValue("BackgroundWorkers:LocalHangfireBackgroundWorker:Cron", Cron.Daily())!;
    }

    [AutomaticRetry(Attempts = 0)]
    //[SkipWhenPreviousJobIsRunning]
    public override Task DoWorkAsync(CancellationToken cancellationToken = default)
    {
        //doing some UOW work
        using (var uow = LazyServiceProvider.LazyGetRequiredService<IUnitOfWorkManager>().Begin())
        {
            Logger.LogInformation("Executed LocalHangfireBackgroundWorker..!");
            return Task.CompletedTask;
        }
    }
}