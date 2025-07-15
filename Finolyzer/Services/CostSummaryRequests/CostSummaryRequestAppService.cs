using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Finolyzer.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System.Linq.Dynamic.Core;
using Finolyzer.Services.Dtos.CostSummaryRequests;
using Finolyzer.Entities;

namespace Finolyzer.Services.CostSummaryRequests;

[Authorize(FinolyzerPermissions.CostSummaryRequests.Default)]
public class CostSummaryRequestAppService : ApplicationService, ICostSummaryRequestAppService
{
    private readonly IRepository<CostSummaryRequest, int> _repository;

    public CostSummaryRequestAppService(IRepository<CostSummaryRequest, int> repository)
    {
        _repository = repository;
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

    [Authorize(FinolyzerPermissions.CostSummaryRequests.Create)]
    public async Task<CostSummaryRequestDto> CreateAsync(CreateUpdateCostSummaryRequestDto input)
    {
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
