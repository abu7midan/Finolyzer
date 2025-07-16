using Finolyzer.Services.Dtos.CostSummaryRequests;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Finolyzer.Pages.CostSummaryRequests;

public class ResultViewerModalModel : AbpPageModel
{
    public CostSummaryRequestResultDto Result { get; set; }

    public override Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var json = TempData["CostSummaryResult"]?.ToString();

        if (!string.IsNullOrWhiteSpace(json))
        {
            Result = JsonSerializer.Deserialize<CostSummaryRequestResultDto>(json);
        }

        return next();
    }
}