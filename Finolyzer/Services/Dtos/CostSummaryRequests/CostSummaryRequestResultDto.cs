using Finolyzer.Entities;
using Finolyzer.Entities.Books;
using System;
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


    public float Total { get; set; }
}
public class CostSummarySystemDependencytDto : AuditedEntityDto<int>
{

    public DependencyType DependencyType { get; set; }
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