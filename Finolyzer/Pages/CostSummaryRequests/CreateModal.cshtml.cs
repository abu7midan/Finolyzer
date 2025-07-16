using Finolyzer.Entities;
using Finolyzer.Services.CostSummaryRequests;
using Finolyzer.Services.Dtos.CostSummaryRequests;
using IdentityModel.OidcClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;
using Volo.Abp.Domain.Repositories;

namespace Finolyzer.Pages.CostSummaryRequests
{
    public class CreateModalModel : AbpPageModel
    {
        [BindProperty]
        public CreateUpdateCostSummaryRequestDto CostSummaryRequest { get; set; }
        public List<SelectListItem> ApplicationSystems { get; set; }
        public List<SelectListItem> Portfolios { get; set; }
        private readonly IRepository<ApplicationSystem, int> _applicationSystemRepository;
        private readonly IRepository<Portfolio, int> _portfolioRepository;
        private readonly ICostSummaryRequestAppService _bookAppService;

        public CreateModalModel(ICostSummaryRequestAppService bookAppService, IRepository<Portfolio, int> portfolioRepository,
        IRepository<ApplicationSystem, int> applicationSystemRepository)
        {
            _bookAppService = bookAppService;
            _applicationSystemRepository = applicationSystemRepository;
            _portfolioRepository = portfolioRepository;
        }

        public async Task OnGetAsync()
        {
            CostSummaryRequest = new CreateUpdateCostSummaryRequestDto();
            var systems = await _applicationSystemRepository.GetListAsync();
            ApplicationSystems = systems
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToList();

            var portfolios = await _portfolioRepository.GetListAsync();
            Portfolios = portfolios
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var result = await _bookAppService.CalculateAsync(CostSummaryRequest);

            // Store in TempData
            TempData["CostSummaryResult"] = JsonSerializer.Serialize(result);

            // Return redirect instruction (client handles it)
            return new JsonResult(new { redirectUrl = Url.Page("/CostSummaryRequests/ResultViewerModal") });
        }
    }
}