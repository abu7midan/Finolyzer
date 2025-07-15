using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Finolyzer.Data;

public class FinolyzerDbContextFactory : IDesignTimeDbContextFactory<FinolyzerDbContext>
{
    public FinolyzerDbContext CreateDbContext(string[] args)
    {
        FinolyzerEfCoreEntityExtensionMappings.Configure();
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<FinolyzerDbContext>()
            .UseSqlServer(configuration.GetConnectionString("Default"));

        return new FinolyzerDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}