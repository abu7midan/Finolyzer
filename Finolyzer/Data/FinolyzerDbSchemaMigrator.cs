using Volo.Abp.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Finolyzer.Data;

public class FinolyzerDbSchemaMigrator : ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public FinolyzerDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        
        /* We intentionally resolving the FinolyzerDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<FinolyzerDbContext>()
            .Database
            .MigrateAsync();

    }
}
