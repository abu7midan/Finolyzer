using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Finolyzer.Services.Dtos.CostSummaryRequests;

namespace Finolyzer.Services.CostSummaryRequests;

public interface ICostSummaryRequestAppService :
    ICrudAppService< //Defines CRUD methods
        CostSummaryRequestDto, //Used to show books
        int, //Primary key of the book entity
        PagedAndSortedResultRequestDto, //Used for paging/sorting
        CreateUpdateCostSummaryRequestDto> //Used to create/update a book
{
    Task<CostSummaryRequestResultDto> CalculateAsync(CreateUpdateCostSummaryRequestDto input);
}