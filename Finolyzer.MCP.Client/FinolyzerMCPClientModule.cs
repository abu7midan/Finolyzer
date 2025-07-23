using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

[DependsOn(typeof(AbpAspNetCoreSerilogModule))]
[DependsOn(typeof(AbpAutofacModule))] //Add dependency to the AbpAutofacModule
public class FinolyzerMCPClientModule : AbpModule
{

}