using Finolyzer.Entities;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Finolyzer.Data;

public class FinolyzerDataSeederContributor
    : IDataSeedContributor, ITransientDependency
{
    //private readonly IRepository<Book, Guid> _bookRepository;
    private readonly IRepository<Portfolio, int> _portfolioRepository;
    private readonly IRepository<ApplicationSystem, int> _applicationSystemRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRepository<Server, int> _serverRepository;
    private readonly IRepository<SystemDependency, int> _systemDependencyRepository;
    private readonly IRepository<IntegrationService, int> _integrationServiceRepository;
    private readonly IRepository<ProviderSubscription, int> _providerSubscriptionRepository;
    private readonly IRepository<SystemIntegrationTransaction, int> _systemIntegrationTransactionRepository;
    private readonly IRepository<Resource, int> _resourceRepository;

    public FinolyzerDataSeederContributor(
        IRepository<Portfolio, int> portfolioRepository,
        IRepository<ApplicationSystem, int> applicationSystemRepository,
        IRepository<Provider, int> providerRepository,
        IRepository<Server, int> server,
        IRepository<SystemDependency, int> systemDependency,
        IRepository<IntegrationService, int> integrationService,
        IRepository<ProviderSubscription, int> providerSubscription,
        IRepository<SystemIntegrationTransaction, int> systemIntegrationTransaction,
        IRepository<Resource, int> resource
        )
    {
        _portfolioRepository = portfolioRepository;
        _applicationSystemRepository = applicationSystemRepository;
        _providerRepository = providerRepository;
        _serverRepository = server;
        _systemDependencyRepository = systemDependency;
        _integrationServiceRepository = integrationService;
        _providerSubscriptionRepository = providerSubscription;
        _systemIntegrationTransactionRepository = systemIntegrationTransaction;
        _resourceRepository = resource;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        Portfolio portfolio = null;
        ApplicationSystem applicationSystem = null;
        Provider providermicrosoft = null;
        Provider providergoogle = null;
        Provider providermsgat = null;
        Server serverWindows = null;
        Server serverLinux = null;
        ProviderSubscription providerSubscription = null;
        Resource resource = null;
        SystemIntegrationTransaction systemIntegrationTransaction = null;
        List<SystemDependency> systemDependencies = new List<SystemDependency>();
        List<IntegrationService> integrationServices = new List<IntegrationService>();

        if (await _portfolioRepository.GetCountAsync() <= 0)
        {
            portfolio = await _portfolioRepository.InsertAsync(
                new Portfolio
                {
                    Name = "Visionary Ventures",
                },
                autoSave: true
            );
        }

        if (await _applicationSystemRepository.GetCountAsync() <= 0)
        {
            applicationSystem = await _applicationSystemRepository.InsertAsync(
                new ApplicationSystem
                {
                    Name = "Finolyzer",
                    Portfolio = portfolio
                },
                autoSave: true
            );
        }

        if (await _providerRepository.GetCountAsync() <= 0)
        {
            providermicrosoft = await _providerRepository.InsertAsync(
                new Provider
                {
                    Name = "microsoft",
                },
                autoSave: true
            );
            providergoogle = await _providerRepository.InsertAsync(
                new Provider
                {
                    Name = "google",
                },
                autoSave: true
            );
            providermsgat = await _providerRepository.InsertAsync(
    new Provider
    {
        Name = "msgat",
    },
    autoSave: true
);
        }

        if (await _integrationServiceRepository.GetCountAsync() <= 0)
        {
            integrationServices.Add(await _integrationServiceRepository.InsertAsync(
                new IntegrationService
                {
                    IntegrationSubscriptionType = IntegrationSubscriptionType.Paid,
                    Description = "Notification",
                    URL = "https://abp.io/docs",
                    Provider = providermsgat,
                    UnitCost = 7,
                    Shared = true,
                },
                autoSave: true
            ));

        }

        if (await _serverRepository.GetCountAsync() <= 0)
        {
            serverWindows = await _serverRepository.InsertAsync(
                new Server
                {
                    Specification = "Windows ram 32 cpu 8 ",
                    Provider = providermicrosoft,
                    YearlyCost = 400,
                    Shared = true,
                },
                autoSave: true
            );
            serverLinux = await _serverRepository.InsertAsync(
                new Server
                {
                    Specification = "RHEL ram 32 cpu 8 ",
                    Provider = providergoogle,
                    YearlyCost = 200,
                    Shared = true,
                },
                autoSave: true
            );
        }

        if (await _providerSubscriptionRepository.GetCountAsync() <= 0)
        {
            providerSubscription = await _providerSubscriptionRepository.InsertAsync(
                new ProviderSubscription
                {
                    Description = "license",
                    URL = "https://abp.io/docs",
                    YearlyCost = 5000,
                    Shared = false,
                    Provider = providermicrosoft,
                },
                autoSave: true
            );
        }
        if (await _resourceRepository.GetCountAsync() <= 0)
        {
            resource = await _resourceRepository.InsertAsync(
                new Resource
                {
                    Name = "abu7midan",
                    NationalId = "5845666",
                    YearlyCost = 20000,
                    Shared = true,
                },
                autoSave: true
            );
        }

        if (await _systemDependencyRepository.GetCountAsync() <= 0)
        {
            foreach (var item in integrationServices)
            {
                systemDependencies.Add(await _systemDependencyRepository.InsertAsync(
                                new SystemDependency
                                {
                                    ApplicationSystem = applicationSystem,
                                    DependencyType = DependencyType.IntegrationService,
                                    IntegrationService = item,
                                    SharePercentage = (item.Shared ? (100 / 1) : 100),
                                    //YearlyCost = item.YearlyCost / (item.Shared ? (100 / 1) : 100),
                                    Year = 2025,
                                    Month = 7,
                                },
                                autoSave: true
                            ));


            }


            systemDependencies.Add(await _systemDependencyRepository.InsertAsync(
                    new SystemDependency
                    {
                        ApplicationSystem = applicationSystem,
                        DependencyType = DependencyType.ProviderSubscription,
                        ProviderSubscription = providerSubscription,
                        SharePercentage = (providerSubscription.Shared ? (100 / 1) : 100),
                        YearlyCost = providerSubscription.YearlyCost / (providerSubscription.Shared ? (100 / 1) : 100),
                        Year = 2025,
                        Month = 7,
                    },
                    autoSave: true
                ));

            systemDependencies.Add(await _systemDependencyRepository.InsertAsync(
        new SystemDependency
        {
            ApplicationSystem = applicationSystem,
            DependencyType = DependencyType.Server,
            Server = serverWindows,
            SharePercentage = (serverWindows.Shared ? (100 / 1) : 100),
            YearlyCost = serverWindows.YearlyCost / (serverWindows.Shared ? (100 / 1) : 100),
        },
        autoSave: true
    ));

            systemDependencies.Add(await _systemDependencyRepository.InsertAsync(
new SystemDependency
{
    ApplicationSystem = applicationSystem,
    DependencyType = DependencyType.Resource,
    Resource = resource,
    SharePercentage = (resource.Shared ? (100 / 1) : 100),
    YearlyCost = resource.YearlyCost / (resource.Shared ? (100 / 1) : 100),
},
autoSave: true
));
        }
        var notificationintegration = integrationServices.FirstOrDefault(x => x.Description == "Notification");
        if (await _systemIntegrationTransactionRepository.GetCountAsync() <= 0)
        {
            systemIntegrationTransaction = await _systemIntegrationTransactionRepository.InsertAsync(
                new SystemIntegrationTransaction
                {
                    Description = "abu7midan",
                    Year = 2025,
                    Month = 7,
                    Day = 15,
                    UsageCount = 15,
                    IntegrationService= notificationintegration,
                    ApplicationSystem=applicationSystem,
                    RequestType=TimlyRequestType.Monthly
                },
                autoSave: true
            );
        }
    }
}