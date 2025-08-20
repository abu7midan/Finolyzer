

using Finolyzer.Jobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
public class LocalPeriodicWorker : AsyncPeriodicBackgroundWorkerBase
{
    public LocalPeriodicWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory, IConfiguration configuration)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = (int)configuration.GetValue("BackgroundWorkers:LocalPeriodicWorker:Period", TimeSpan.FromSeconds(20)).TotalMilliseconds;
    }

    protected override Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        //resolve dependencies using PeriodicBackgroundWorkerContext - not constructor injection
        //var service = workerContext.ServiceProvider.GetRequiredService<IService>();

        Logger.LogInformation("LocalPeriodicWorker is working...");

        return Task.CompletedTask;
    }
}