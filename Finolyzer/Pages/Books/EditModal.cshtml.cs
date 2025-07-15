using System;
using System.Threading.Tasks;
using Finolyzer.Services.Books;
using Finolyzer.Services.Dtos.Books;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Finolyzer.Pages.Books;

public class EditModalModel : AbpPageModel
{
    [HiddenInput]
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public CreateUpdateCostSummaryRequestDto Book { get; set; }

    private readonly ICostSummaryRequestAppService _bookAppService;

    public EditModalModel(ICostSummaryRequestAppService bookAppService)
    {
        _bookAppService = bookAppService;
    }

    public async Task OnGetAsync()
    {
        var bookDto = await _bookAppService.GetAsync(Id);
        Book = ObjectMapper.Map<CostSummaryRequestDto, CreateUpdateCostSummaryRequestDto>(bookDto);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _bookAppService.UpdateAsync(Id, Book);
        return NoContent();
    }
}