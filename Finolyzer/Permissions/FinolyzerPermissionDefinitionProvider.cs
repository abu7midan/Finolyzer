using Finolyzer.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace Finolyzer.Permissions;

public class FinolyzerPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(FinolyzerPermissions.GroupName);


        var booksPermission = myGroup.AddPermission(FinolyzerPermissions.Books.Default, L("Permission:Books"));
        booksPermission.AddChild(FinolyzerPermissions.Books.Create, L("Permission:Books.Create"));
        booksPermission.AddChild(FinolyzerPermissions.Books.Edit, L("Permission:Books.Edit"));
        booksPermission.AddChild(FinolyzerPermissions.Books.Delete, L("Permission:Books.Delete"));
        
        //Define your own permissions here. Example:
        //myGroup.AddPermission(FinolyzerPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<FinolyzerResource>(name);
    }
}
