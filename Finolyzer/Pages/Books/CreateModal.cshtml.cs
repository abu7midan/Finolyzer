using System.Threading.Tasks;
using Finolyzer.Services.Books;
using Finolyzer.Services.Dtos.Books;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Finolyzer.Pages.Books
{
    public class CreateModalModel : AbpPageModel
    {
        [BindProperty]
        public CreateUpdateCostSummaryRequestDto Book { get; set; }

        private readonly ICostSummaryRequestAppService _bookAppService;

        public CreateModalModel(ICostSummaryRequestAppService bookAppService)
        {
            _bookAppService = bookAppService;
        }

        public void OnGet()
        {
            Book = new CreateUpdateCostSummaryRequestDto();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await _bookAppService.CreateAsync(Book);
            return NoContent();
        }
    }
}