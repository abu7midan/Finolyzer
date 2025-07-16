using Finolyzer.Entities;
using Finolyzer.Permissions;
using Finolyzer.Services.Dtos.CostSummaryRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace Finolyzer.Services.CostSummaryRequests;

//[Authorize(FinolyzerPermissions.CostSummaryRequests.Default)]
public class CostSummaryRequestAppService : ApplicationService, ICostSummaryRequestAppService
{
    private readonly IRepository<CostSummaryRequest, int> _repository;
    private readonly IRepository<Portfolio, int> _portfolioRepository;
    private readonly IRepository<ApplicationSystem, int> _applicationSystemRepository;
    private readonly IRepository<Provider, int> _providerRepository;
    private readonly IRepository<Server, int> _serverRepository;
    private readonly IRepository<SystemDependency, int> _systemDependencyRepository;
    private readonly IRepository<IntegrationService, int> _integrationServiceRepository;
    private readonly IRepository<ProviderSubscription, int> _providerSubscriptionRepository;
    private readonly IRepository<SystemIntegrationTransaction, int> _systemIntegrationTransactionRepository;
    private readonly IRepository<Resource, int> _resourceRepository;
    private readonly IRepository<SharedService, int> _sharedServiceRepository;
    public CostSummaryRequestAppService(IRepository<CostSummaryRequest, int> repository,

        IRepository<Portfolio, int> portfolioRepository,
        IRepository<ApplicationSystem, int> applicationSystemRepository,
        IRepository<Provider, int> providerRepository,
        IRepository<Server, int> server,
        IRepository<SystemDependency, int> systemDependency,
        IRepository<IntegrationService, int> integrationService,
        IRepository<ProviderSubscription, int> providerSubscription,
        IRepository<SystemIntegrationTransaction, int> systemIntegrationTransaction,
        IRepository<Resource, int> resource,
        IRepository<SharedService, int> sharedServiceRepository)
    {
        _repository = repository;
        _portfolioRepository = portfolioRepository;
        _applicationSystemRepository = applicationSystemRepository;
        _providerRepository = providerRepository;
        _serverRepository = server;
        _systemDependencyRepository = systemDependency;
        _integrationServiceRepository = integrationService;
        _providerSubscriptionRepository = providerSubscription;
        _systemIntegrationTransactionRepository = systemIntegrationTransaction;
        _resourceRepository = resource;
        _sharedServiceRepository = sharedServiceRepository;
    }

    public async Task<CostSummaryRequestDto> GetAsync(int id)
    {
        var CostSummaryRequest = await _repository.GetAsync(id);
        return ObjectMapper.Map<CostSummaryRequest, CostSummaryRequestDto>(CostSummaryRequest);
    }

    public async Task<PagedResultDto<CostSummaryRequestDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var query = queryable
            .OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "CreationTime" : input.Sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount);

        var CostSummaryRequests = await AsyncExecuter.ToListAsync(query);
        var totalCount = await AsyncExecuter.CountAsync(queryable);

        return new PagedResultDto<CostSummaryRequestDto>(
            totalCount,
            ObjectMapper.Map<List<CostSummaryRequest>, List<CostSummaryRequestDto>>(CostSummaryRequests)
        );
    }
    public async Task<CostSummaryRequestDto> CreateAsync(CreateUpdateCostSummaryRequestDto input)
    {
        var CostSummaryRequest = ObjectMapper.Map<CreateUpdateCostSummaryRequestDto, CostSummaryRequest>(input);
        await _repository.InsertAsync(CostSummaryRequest);
        return ObjectMapper.Map<CostSummaryRequest, CostSummaryRequestDto>(CostSummaryRequest);
    }
    //[Authorize(FinolyzerPermissions.CostSummaryRequests.Create)]
    public async Task<CostSummaryRequestResultDto> CalculateAsync(CreateUpdateCostSummaryRequestDto input)
    {
       

            CostSummaryRequestResultDto result = new CostSummaryRequestResultDto();
            var sharePercentageSharedServices = 100 / (await _applicationSystemRepository.GetCountAsync());

            if (input.CalculationFor == CalculationForType.Portfolio)
            {
                //portfolio = await _portfolioRepository.GetAsync(input.EntityId);
            }
            else
            {
                ApplicationSystem applicationSystem = await _applicationSystemRepository.GetAsync(input.EntityId, true) ??
                  throw new UserFriendlyException(FinolyzerErrorCodes.ApplicationSystemNotExists);

                result.PortfolioName = applicationSystem.Portfolio.Name;
                result.SystemName = applicationSystem.Name;
                result.TimlyRequestType = input.TimlyRequestType;
                result.CalculationBeforeDate = input.CalculationBeforeDate;
                int currentmonth = input.CalculationBeforeDate.Month;

                if (input.TimlyRequestType == TimlyRequestType.Monthly)
                {


                    foreach (SystemDependency dependency in applicationSystem.SystemDependencies.Where(x => x.Year == input.CalculationBeforeDate.Year && x.Month <= input.CalculationBeforeDate.Month))
                    {
                        if (dependency.DependencyType == DependencyType.Server)
                        {
                            var serverCost = CalculateSharedCost(dependency.Server.MonthlyCost, currentmonth, dependency.SharePercentage, dependency.Server.Shared);
                            //float? serverCost = dependency.Server.Shared ? (currentmonth * dependency.Server.MonthlyCost) * (dependency.SharePercentage / 100) : currentmonth * dependency.Server.MonthlyCost;
                            result.SystemDependencies.Add(new CostSummarySystemDependencytDto()
                            {
                                DependencyType = DependencyType.Server.ToString(),
                                ServerName = $"{dependency.Server.Provider.Name}-{dependency.Server.Specification}",
                                ProviderName = $"{dependency.Server.Provider.Name}",
                                Shared = dependency.Server.Shared ? "Yes" : "No",
                                SharePercentage = $"{dependency.SharePercentage.ToString()}% ",
                                TotalCost = Convert.ToDouble(serverCost),
                                TotalCustomCost = Convert.ToDouble(0),
                            });

                        }
                        else if (dependency.DependencyType == DependencyType.IntegrationService)
                        {
                            List<SystemIntegrationTransaction> systemIntegrationTransactions = await _systemIntegrationTransactionRepository
                                .GetListAsync(x => x.IntegrationServiceId == dependency.IntegrationServiceId
                                && x.ApplicationSystemId == applicationSystem.Id && x.Year == input.CalculationBeforeDate.Year && x.Month <= input.CalculationBeforeDate.Month, true);
                            //x.IntegrationService.IntegrationSubscriptionType == IntegrationSubscriptionType.Paid &&
                            double? integrationCost = systemIntegrationTransactions?.Where(x => x.IntegrationService.IntegrationSubscriptionType == IntegrationSubscriptionType.Paid).Sum(x => x.UsageCount * x.IntegrationService.UnitCost) ?? 0;
                            double? integrationUsageCount = systemIntegrationTransactions?.Sum(x => x.UsageCount) ?? 0;

                            var firstLog = systemIntegrationTransactions.FirstOrDefault();

                            if (firstLog == null) continue; // Add this to prevent null ref
                            result.IntegrationTransactions.Add(new CostSummaryIntegrationTransactionDto()
                            {
                                ServiceName = $"{systemIntegrationTransactions.FirstOrDefault().IntegrationService.Provider.Name}-{systemIntegrationTransactions.FirstOrDefault().IntegrationService.Description}",
                                ProviderName = $"{systemIntegrationTransactions.FirstOrDefault().IntegrationService.Provider.Name}",
                                Shared = systemIntegrationTransactions.FirstOrDefault().IntegrationService.Shared ? "Yes" : "No",
                                PaymentType = $"{systemIntegrationTransactions.FirstOrDefault().IntegrationService.IntegrationSubscriptionType.ToString()}% ",
                                SharePercentage = $"{dependency.SharePercentage.ToString()}% ",
                                TotalUsageCount = Convert.ToDouble(integrationUsageCount),
                                TotalCost = Convert.ToDouble(integrationCost),
                                TotalCustomCost = Convert.ToDouble(0),
                            });

                        }
                        else if (dependency.DependencyType == DependencyType.ProviderSubscription)
                        {
                            float? serverCost = dependency.ProviderSubscription.Shared ? (currentmonth * dependency.ProviderSubscription.MonthlyCost) * (dependency.SharePercentage / 100) : currentmonth * dependency.ProviderSubscription.MonthlyCost;

                            result.SystemDependencies.Add(new CostSummarySystemDependencytDto()
                            {
                                DependencyType = DependencyType.ProviderSubscription.ToString(),
                                ProviderSubscriptionName = $"{dependency.ProviderSubscription.Provider.Name}-{dependency.ProviderSubscription.Description}",
                                ProviderName = $"{dependency.ProviderSubscription.Provider.Name}",
                                Shared = dependency.ProviderSubscription.Shared ? "Yes" : "No",
                                SharePercentage = $"{dependency.SharePercentage.ToString()}% ",
                                TotalCost = Convert.ToDouble(serverCost),
                                TotalCustomCost = Convert.ToDouble(0),
                            });
                        }
                        else if (dependency.DependencyType == DependencyType.Resource)
                        {
                            float? serverCost = dependency.Resource.Shared ? (currentmonth * dependency.Resource.MonthlyCost) * (dependency.SharePercentage / 100) : currentmonth * dependency.Resource.MonthlyCost;

                            result.SystemDependencies.Add(new CostSummarySystemDependencytDto()
                            {
                                DependencyType = DependencyType.Resource.ToString(),
                                ResourcenName = $"{dependency.Resource.Name}",
                                Shared = dependency.Resource.Shared ? "Yes" : "No",
                                SharePercentage = $"{dependency.SharePercentage.ToString()}% ",
                                TotalCost = Convert.ToDouble(serverCost),
                                TotalCustomCost = Convert.ToDouble(0),
                            });
                        }
                    }
                    //var costInfra = applicationSystem.SystemDependencies
                    //    .Where(x => x.Year == input.CalculationBeforeDate.Year && x.Month <= input.CalculationBeforeDate.Month)?
                    //    .Sum(d => d.Server?.YearlyCost ?? 0 + d.Resource?.YearlyCost ?? 0 + d.ProviderSubscription?.YearlyCost ?? 0
                    //+ d.IntegrationService?.YearlyCost ?? 0);

                }
                var sharedServices = await _sharedServiceRepository.GetListAsync(true);
                foreach (var sharedService in sharedServices)
                {
                    float? serverCost = sharedService.Shared ? (currentmonth * sharedService.MonthlyCost) * (sharePercentageSharedServices / 100) : currentmonth * sharedService.MonthlyCost;

                    result.SharedServices.Add(new CostSummarySharedServiceDto()
                    {
                        Name = $"{sharedService.Provider.Name}-{sharedService.Name}",
                        PorviderName = sharedService.Provider.Name,
                        SharePercentage = sharePercentageSharedServices,
                        Shared = sharedService.Shared ? "Yes" : "No",
                        TotalCost = Convert.ToDouble(serverCost),
                        TotalCustomCost = Convert.ToDouble(0),
                    });
                }

            }

            return result;

    }
    private double CalculateSharedCost(float? monthlyCost, int monthCount, float sharePercentage, bool shared)
    {
        var baseCost = (monthlyCost ?? 0) * monthCount;
        return shared ? (double)(baseCost * (sharePercentage / 100f)) : (double)baseCost;
    }
    [Authorize(FinolyzerPermissions.CostSummaryRequests.Edit)]
    public async Task<CostSummaryRequestDto> UpdateAsync(int id, CreateUpdateCostSummaryRequestDto input)
    {
        var CostSummaryRequest = await _repository.GetAsync(id);
        ObjectMapper.Map(input, CostSummaryRequest);
        await _repository.UpdateAsync(CostSummaryRequest);
        return ObjectMapper.Map<CostSummaryRequest, CostSummaryRequestDto>(CostSummaryRequest);
    }

    [Authorize(FinolyzerPermissions.CostSummaryRequests.Delete)]
    public async Task DeleteAsync(int id)
    {
        await _repository.DeleteAsync(id);
    }
}
