using Volo.Abp.Application.Services;
using Finolyzer.Localization;

namespace Finolyzer.Services;

/* Inherit your application services from this class. */
public abstract class FinolyzerAppService : ApplicationService
{
    protected FinolyzerAppService()
    {
        LocalizationResource = typeof(FinolyzerResource);
    }
}