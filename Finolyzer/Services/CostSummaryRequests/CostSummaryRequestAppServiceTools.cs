using Finolyzer.Services.Dtos.CostSummaryRequests;

namespace Finolyzer.Services.CostSummaryRequests;

using Finolyzer.Entities;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

[McpServerToolType]
public class CostSummaryRequestAppServiceTools : ApplicationService
{
    private readonly ICostSummaryRequestAppService _service;

    public CostSummaryRequestAppServiceTools(ICostSummaryRequestAppService service)
    {
        _service = service;
    }


    [McpServerTool, Description("Calculate System cost by input")]
    public async Task<CostSummaryRequestResultDto> CalculateSystemCost(string description,string notes,int entityId,TimlyRequestType timlyRequestType,CalculationForType calculationFor,DateTime calculationBeforeDate,bool includeSharedService = true)
    {
        var request = new CreateUpdateCostSummaryRequestDto
        {
            Description = description,
            Notes = notes,
            EntityId = entityId,
            TimlyRequestType = timlyRequestType,
            CalculationFor = calculationFor,
            CalculationBeforeDate = calculationBeforeDate,
            IncludeSharedService = includeSharedService
        };

        return await _service.CalculateAsync(request);
    }



}