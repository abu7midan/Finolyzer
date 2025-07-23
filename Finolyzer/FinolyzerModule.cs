using Finolyzer.Data;
using Finolyzer.Entities;
using Finolyzer.HealthChecks;
using Finolyzer.Localization;
using Finolyzer.Menus;
using Finolyzer.Permissions;
using Finolyzer.Services.CostSummaryRequests;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Toolbars;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Caching;
using Volo.Abp.Emailing;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.DependencyInjection;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.FeatureManagement;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.Identity.Web;
using Volo.Abp.Localization;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.HttpApi;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.PermissionManagement.OpenIddict;
using Volo.Abp.PermissionManagement.Web;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.Web;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.Validation.Localization;
using Volo.Abp.VirtualFileSystem;

namespace Finolyzer;

[DependsOn(
    // ABP Framework packages
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpAutofacModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpCachingModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpStudioClientAspNetCoreModule),

    // lepton-theme
    typeof(AbpAspNetCoreMvcUiBasicThemeModule),
     
    //// Account module packages
    //typeof(AbpAccountWebOpenIddictModule),
    //typeof(AbpAccountHttpApiModule),
    //typeof(AbpAccountApplicationModule),

    //// Identity module packages
    //typeof(AbpPermissionManagementDomainIdentityModule),
    //typeof(AbpPermissionManagementDomainOpenIddictModule),
    //typeof(AbpIdentityWebModule),
    //typeof(AbpIdentityHttpApiModule),
    //typeof(AbpIdentityApplicationModule),

    //// Permission Management module packages
    //typeof(AbpPermissionManagementWebModule),
    //typeof(AbpPermissionManagementApplicationModule),
    //typeof(AbpPermissionManagementHttpApiModule),

    //// Feature Management module packages
    //typeof(AbpFeatureManagementWebModule),
    //typeof(AbpFeatureManagementHttpApiModule),
    //typeof(AbpFeatureManagementApplicationModule),

    // Setting Management module packages
    //typeof(AbpSettingManagementWebModule),
    //typeof(AbpSettingManagementHttpApiModule),
    //typeof(AbpSettingManagementApplicationModule),

    // Entity Framework Core packages for the used modules
    typeof(AbpAuditLoggingEntityFrameworkCoreModule),
    //typeof(AbpFeatureManagementEntityFrameworkCoreModule),
    //typeof(AbpIdentityEntityFrameworkCoreModule),
    //typeof(AbpOpenIddictEntityFrameworkCoreModule),
    //typeof(AbpPermissionManagementEntityFrameworkCoreModule),
    //typeof(AbpSettingManagementEntityFrameworkCoreModule),
    //typeof(AbpBackgroundJobsEntityFrameworkCoreModule),
    //typeof(BlobStoringDatabaseEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqlServerModule)
)]
public class FinolyzerModule : AbpModule
{
    /* Single point to enable/disable multi-tenancy */
    public const bool IsMultiTenant = false;

    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(
                typeof(FinolyzerResource)
            );
        });

        //PreConfigure<OpenIddictBuilder>(builder =>
        //{
        //    builder.AddValidation(options =>
        //    {
        //        options.AddAudiences("Finolyzer");
        //        options.UseLocalServer();
        //        options.UseAspNetCore();
        //    });
        //});

        //if (!hostingEnvironment.IsDevelopment())
        //{
        //    PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
        //    {
        //        options.AddDevelopmentEncryptionAndSigningCertificate = false;
        //    });

        //    PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
        //    {
        //        serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx", configuration["AuthServer:CertificatePassPhrase"]!);
        //    });
        //}

        FinolyzerGlobalFeatureConfigurator.Configure();
        FinolyzerModuleExtensionConfigurator.Configure();
        FinolyzerEfCoreEntityExtensionMappings.Configure();
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        if (hostingEnvironment.IsDevelopment())
        {
            context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
        }
        ConfigureMCPServer(context);
        //ConfigureAuthentication(context);
        //ConfigureMultiTenancy();
        ConfigureUrls(configuration);
        ConfigureBundles();
        ConfigureHealthChecks(context);
        ConfigureAutoMapper(context);
        ConfigureSwagger(context.Services);
        ConfigureAutoApiControllers();
        ConfigureVirtualFiles(hostingEnvironment);
        ConfigureLocalization();
        ConfigureNavigationServices();
        ConfigureEfCore(context);

        Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AuthorizePage("/Books/Index", FinolyzerPermissions.Books.Default);
            options.Conventions.AuthorizePage("/Books/CreateModal", FinolyzerPermissions.Books.Create);
            options.Conventions.AuthorizePage("/Books/EditModal", FinolyzerPermissions.Books.Edit);
        });
    }
    private void ConfigureMCPServer(ServiceConfigurationContext context)
    {
        context.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(CostSummaryRequestAppServiceTools).Assembly);
        context.Services.AddTransient<CostSummaryRequestAppServiceTools>();
    }
    private void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddFinolyzerHealthChecks();
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    private void ConfigureMultiTenancy()
    {
        Configure<AbpMultiTenancyOptions>(options =>
        {
            options.IsEnabled = IsMultiTenant;
        });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
        });
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                BasicThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );

            options.ScriptBundles.Configure(
                BasicThemeBundles.Scripts.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-scripts.js");
                }
            );
        });
    }

    private void ConfigureLocalization()
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<FinolyzerResource>("en-AE")
                .AddBaseTypes(typeof(AbpValidationResource))
                .AddVirtualJson("/Localization/Finolyzer");

            options.DefaultResourceType = typeof(FinolyzerResource);

            options.Languages.Add(new LanguageInfo("en-AE", "en-AE", "English (United Arab Emirates)"));

        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace("Finolyzer", typeof(FinolyzerResource));
        });
    }

    private void ConfigureVirtualFiles(IWebHostEnvironment hostingEnvironment)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<FinolyzerModule>();
            if (hostingEnvironment.IsDevelopment())
            {
                /* Using physical files in development, so we don't need to recompile on changes */
                options.FileSets.ReplaceEmbeddedByPhysical<FinolyzerModule>(hostingEnvironment.ContentRootPath);
            }
        });
    }

    private void ConfigureAutoApiControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(FinolyzerModule).Assembly);
        });
    }

    private void ConfigureSwagger(IServiceCollection services)
    {
        services.AddAbpSwaggerGen(
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Finolyzer API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            }
        );
    }

    private void ConfigureAutoMapper(ServiceConfigurationContext context)
    {
        context.Services.AddAutoMapperObjectMapper<FinolyzerModule>();
        Configure<AbpAutoMapperOptions>(options =>
        {
            /* Uncomment `validate: true` if you want to enable the Configuration Validation feature.
             * See AutoMapper's documentation to learn what it is:
             * https://docs.automapper.org/en/stable/Configuration-validation.html
             */
            options.AddMaps<FinolyzerModule>(/* validate: true */);
        });
    }


    private void ConfigureNavigationServices()
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new FinolyzerMenuContributor());
        });

        Configure<AbpToolbarOptions>(options =>
        {
            options.Contributors.Add(new FinolyzerToolbarContributor());
        });
    }

    private void ConfigureEfCore(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<FinolyzerDbContext>(options =>
        {
            /* You can remove "includeAllEntities: true" to create
             * default repositories only for aggregate roots
             * Documentation: https://docs.abp.io/en/abp/latest/Entity-Framework-Core#add-default-repositories
             */
            options.AddDefaultRepositories(includeAllEntities: true);

        });

        Configure<AbpDbContextOptions>(options =>
        {
            //options.PreConfigure<FinolyzerDbContext>(opts =>
            //{
            //    opts.DbContextOptions.UseLazyLoadingProxies(); //Enable lazy loading
            //});
            options.Configure(configurationContext =>
            {
                configurationContext.UseSqlServer();
            });
        });
        Configure<AbpEntityOptions>(options =>
        {
            options.Entity<ApplicationSystem>(options =>
            {
                options.DefaultWithDetailsFunc = query => query
                .Include(o => o.Portfolio)
                .Include(o => o.SystemDependencies).ThenInclude(x => x.IntegrationService).ThenInclude(x => x.Provider)
                .Include(x => x.SystemDependencies).ThenInclude(x => x.ProviderSubscription).ThenInclude(x => x.Provider)
                .Include(x => x.SystemDependencies).ThenInclude(x => x.Server).ThenInclude(x => x.Provider)
                .Include(x => x.SystemDependencies).ThenInclude(x => x.Resource);
            });

            options.Entity<SharedService>(options =>
            {
                options.DefaultWithDetailsFunc = query => query
                .Include(o => o.Provider)
                .Include(o => o.SystemDependencies).ThenInclude(x => x.IntegrationService).ThenInclude(x => x.Provider)
                .Include(x => x.SystemDependencies).ThenInclude(x => x.ProviderSubscription).ThenInclude(x => x.Provider)
                .Include(x => x.SystemDependencies).ThenInclude(x => x.Server).ThenInclude(x => x.Provider)
                .Include(x => x.SystemDependencies).ThenInclude(x => x.Resource);
            });

        });
    }


    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
        }

        app.UseCorrelationId();
        app.UseRouting();
        app.MapAbpStaticAssets();
        app.UseAbpStudioLink();
        app.UseAbpSecurityHeaders();
        //app.UseAuthentication();
        //app.UseAbpOpenIddictValidation();

        //if (IsMultiTenant)
        //{
        //    app.UseMultiTenancy();
        //}

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        //app.UseAuthorization();

        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Finolyzer API");
        });

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }
}
