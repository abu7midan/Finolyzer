using Finolyzer.Permissions;
using Finolyzer.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity.Web.Navigation;
using Volo.Abp.SettingManagement.Web.Navigation;
using Volo.Abp.UI.Navigation;
using Volo.Abp.Identity.Web.Navigation;

namespace Finolyzer.Menus;

public class FinolyzerMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
    }

    private static Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<FinolyzerResource>();
        context.Menu.Items.Insert(
            0,
            new ApplicationMenuItem(FinolyzerMenus.Home,l["Menu:Home"],"~/",icon: "fas fa-home",order: 0)
        );
        context.Menu.Items.Insert(
      1,
      new ApplicationMenuItem(FinolyzerMenus.Home, l["Menu:CostCalc"], "~/CostSummaryRequests", icon: "fa fa-money", order: 0)
  );

        //Administration
        var administration = context.Menu.GetAdministration();
        administration.Order = 5;
        //Administration->Identity
        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 2);

        //Administration->Settings
        administration.SetSubItemOrder(SettingManagementMenuNames.GroupName, 7);
    
        context.Menu.AddItem(
            new ApplicationMenuItem(
                "BooksStore",
                l["Menu:Finolyzer"],
                icon: "fa fa-book"
            ).AddItem(new ApplicationMenuItem("BooksStore.Books",l["Menu:Books"],url: "/Books").RequirePermissions(FinolyzerPermissions.Books.Default))
            );
        
        return Task.CompletedTask;
    }
}
