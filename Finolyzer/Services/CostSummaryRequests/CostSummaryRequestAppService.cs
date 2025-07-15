using Finolyzer.Entities;
using Finolyzer.Permissions;
using Finolyzer.Services.Dtos.CostSummaryRequests;
using Microsoft.AspNetCore.Authorization;
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
    public CostSummaryRequestAppService(IRepository<CostSummaryRequest, int> repository,

        IRepository<Portfolio, int> portfolioRepository,
        IRepository<ApplicationSystem, int> applicationSystemRepository,
        IRepository<Provider, int> providerRepository,
        IRepository<Server, int> server,
        IRepository<SystemDependency, int> systemDependency,
        IRepository<IntegrationService, int> integrationService,
        IRepository<ProviderSubscription, int> providerSubscription,
        IRepository<SystemIntegrationTransaction, int> systemIntegrationTransaction,
        IRepository<Resource, int> resource)
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
            .OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "Name" : input.Sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount);

        var CostSummaryRequests = await AsyncExecuter.ToListAsync(query);
        var totalCount = await AsyncExecuter.CountAsync(queryable);

        return new PagedResultDto<CostSummaryRequestDto>(
            totalCount,
            ObjectMapper.Map<List<CostSummaryRequest>, List<CostSummaryRequestDto>>(CostSummaryRequests)
        );
    }

    //[Authorize(FinolyzerPermissions.CostSummaryRequests.Create)]
    public async Task<CostSummaryRequestDto> CreateAsync(CreateUpdateCostSummaryRequestDto input)
    {
        if (input.CalculationFor == CalculationForType.Portfolio)
        {
            //portfolio = await _portfolioRepository.GetAsync(input.EntityId);
        }
        else
        {
            ApplicationSystem applicationSystem = await _applicationSystemRepository.GetAsync(input.EntityId, true) ??
              throw new UserFriendlyException(FinolyzerErrorCodes.ApplicationSystemNotExists);

            if (input.TimlyRequestType == TimlyRequestType.Monthly)
            {
                int currentmonth = input.CalculationBeforeDate.Month;

                foreach (SystemDependency dependency in applicationSystem.SystemDependencies.Where(x => x.Year == input.CalculationBeforeDate.Year && x.Month <= input.CalculationBeforeDate.Month))
                {
                    if (dependency.DependencyType == DependencyType.Server)
                    {
                        float? serverCost = dependency.Server.Shared ? ((currentmonth * dependency.Server.MonthlyCost) / (dependency.SharePercentage / 100)) : (currentmonth * dependency.Server.MonthlyCost);
                    }
                    else if (dependency.DependencyType == DependencyType.IntegrationService)
                    {
                        List<SystemIntegrationTransaction> systemIntegrationTransactions = await _systemIntegrationTransactionRepository.GetListAsync(x => x.ApplicationSystemId == applicationSystem.Id && x.IntegrationService.IntegrationSubscriptionType == IntegrationSubscriptionType.Paid && x.Year == input.CalculationBeforeDate.Year && x.Month <= input.CalculationBeforeDate.Month, true);
                        double? integrationCost = systemIntegrationTransactions?.Sum(x => x.UsageCount * x.IntegrationService.UnitCost) ?? 0;

                    }
                    else if (dependency.DependencyType == DependencyType.ProviderSubscription)
                    {
                    }
                    else if (dependency.DependencyType == DependencyType.Resource)
                    {
                    }
                }
                //var costInfra = applicationSystem.SystemDependencies
                //    .Where(x => x.Year == input.CalculationBeforeDate.Year && x.Month <= input.CalculationBeforeDate.Month)?
                //    .Sum(d => d.Server?.YearlyCost ?? 0 + d.Resource?.YearlyCost ?? 0 + d.ProviderSubscription?.YearlyCost ?? 0
                //+ d.IntegrationService?.YearlyCost ?? 0);

            }
        }



        var CostSummaryRequest = ObjectMapper.Map<CreateUpdateCostSummaryRequestDto, CostSummaryRequest>(input);
        await _repository.InsertAsync(CostSummaryRequest);
        return ObjectMapper.Map<CostSummaryRequest, CostSummaryRequestDto>(CostSummaryRequest);
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
