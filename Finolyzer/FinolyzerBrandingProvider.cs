using Microsoft.Extensions.Localization;
using Finolyzer.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace Finolyzer;

[Dependency(ReplaceServices = true)]
public class FinolyzerBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<FinolyzerResource> _localizer;

    public FinolyzerBrandingProvider(IStringLocalizer<FinolyzerResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}