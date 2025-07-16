using Finolyzer.Entities;
using Volo.Abp.Application.Dtos;

namespace Finolyzer.Services.Dtos.CostSummaryRequests;

public class CostSummaryRequestResultDto : AuditedEntityDto<int>
{
    public string PortfolioName { get; set; }
    public string SystemName { get; set; }

    public TimlyRequestType TimlyRequestType { get; set; }

    public DateTime CalculationBeforeDate { get; set; }
    public List<CostSummarySystemDependencytDto> SystemDependencies { get; set; } = new List<CostSummarySystemDependencytDto>();
    public List<CostSummaryIntegrationTransactionDto> IntegrationTransactions { get; set; } = new List<CostSummaryIntegrationTransactionDto>();
    public List<CostSummarySharedServiceDto> SharedServices { get; set; } = new List<CostSummarySharedServiceDto>();

    public double TotalSystemDependenciesCost => SystemDependencies?.Sum(d => d.TotalCost) ?? 0;
    public double TotalIntegrationTransactionsCost => IntegrationTransactions?.Sum(d => d.TotalCost) ?? 0;
    public double TotalSharedServicesCost => SharedServices?.Sum(d => d.TotalCost) ?? 0;

    public double TotalSystemDependenciesCustomCost => SystemDependencies?.Sum(d => d.TotalCustomCost) ?? 0;
    public double TotalIntegrationTransactionsCustomCost => IntegrationTransactions?.Sum(d => d.TotalCustomCost) ?? 0;
    public double TotalSharedServicesCustomCost => SharedServices?.Sum(d => d.TotalCustomCost) ?? 0;
    public double TotalCost => TotalSharedServicesCost + TotalIntegrationTransactionsCost + TotalSystemDependenciesCost;
    public double TotalCustomCost => TotalSharedServicesCustomCost + TotalIntegrationTransactionsCustomCost + TotalSystemDependenciesCustomCost;
}
public class CostSummarySystemDependencytDto : AuditedEntityDto<int>
{

    public string DependencyType { get; set; }
    public string ResourcenName { get; set; }
    public string ProviderSubscriptionName { get; set; }
    public string ProviderName { get; set; }
    public string ServerName { get; set; }

    public string ServiceName { get; set; }
    public string PorviderName { get; set; }
    public string SharePercentage { get; set; }
    public string Shared { get; set; }
    public double TotalCost { get; set; }
    public double TotalCustomCost { get; set; }

}
public class CostSummaryIntegrationTransactionDto : AuditedEntityDto<int>
{
    public string ProviderName { get; set; }
    public string ServiceName { get; set; }
    public string PorviderName { get; set; }
    public string PaymentType { get; set; }
    public string SharePercentage { get; set; }
    public string Shared { get; set; }
    public double TotalCost { get; set; }
    public double TotalCustomCost { get; set; }
    public double TotalUsageCount { get; set; }


}
public class CostSummarySharedServiceDto : AuditedEntityDto<int>
{
    public string Name { get; set; }
    public string PorviderName { get; set; }
    public double SharePercentage { get; set; }
    public string Shared { get; set; }
    public double TotalCost { get; set; }
    public double TotalCustomCost { get; set; }


}