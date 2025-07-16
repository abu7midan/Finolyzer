namespace Finolyzer.Entities;
using System;
using Volo.Abp.Domain.Entities.Auditing;


public interface ICost
{
    float? DailyCost { get; }
    float? MonthlyCost { get; }
    float? YearlyCost { get; set; }

    ICollection<CostItem>? CustomCosts { get; set; }

    float CustomDailyTotalCost => CustomCosts?.Sum(c => c.DailyCost) ?? 0;
    float CustomMonthlyTotalCost => CustomCosts?.Sum(c => c.MonthlyCost) ?? 0;
    float CustomYearlyTotalCost => CustomCosts?.Sum(c => c.YearlyCost) ?? 0;
}

public class BaseCostEntity : AuditedAggregateRoot<int>, ICost
{

    // Can be set manually or calculated in derived classes
    public virtual float? YearlyCost { get; set; } = 0;

    // Calculated from YearlyCost

    public virtual float? DailyCost => YearlyCost.HasValue ? YearlyCost.Value / 365 : null;
    public virtual float? MonthlyCost => YearlyCost.HasValue ? YearlyCost.Value / 12 : null;

    // Custom costs - collection of detailed components
    public virtual ICollection<CostItem>? CustomCosts { get; set; } = new List<CostItem>();
    public virtual float? CustomDailyTotalCost => CustomCosts?.Sum(c => c.DailyCost) ?? 0;
    public virtual float? CustomMonthlyTotalCost => CustomCosts?.Sum(c => c.MonthlyCost) ?? 0;
    public virtual float? CustomYearlyTotalCost => CustomCosts?.Sum(c => c.YearlyCost) ?? 0;

    public virtual bool Shared { get; set; }


}
public class CostItem
{
    public virtual float? YearlyCost { get; set; } = 0;

    public virtual float? DailyCost => YearlyCost.HasValue ? YearlyCost.Value / 365 : null;
    public virtual float? MonthlyCost => YearlyCost.HasValue ? YearlyCost.Value / 12 : null;
    public string Description { get; set; } = string.Empty;

}


public class CostSummaryRequest : AuditedAggregateRoot<int>
{
    public string Description { get; set; } = string.Empty;

    public Portfolio Portfolio { get; set; }
    public int PortfolioId { get; set; }

    public ApplicationSystem ApplicationSystem { get; set; }
    public int ApplicationSystemId { get; set; }

    public int Quarter { get; set; }
    //(Month - 1) / 3 + 1;
    public int Year { get; set; }
    public int Month { get; set; }
    public TimlyRequestType RequestType { get; set; }
    public float TotalYearlyCost { get; set; }

    public float TotalQuarterCost { get; set; }
    public float TotalMonthlyCost { get; set; }
    public float TotalDailyCost { get; set; }

    public float CustomCost { get; set; }
    public float TransactionCost { get; set; }

    public string? Notes { get; set; }

}


public class SystemDependency : BaseCostEntity
{
    public int ApplicationSystemId { get; set; }
    public ApplicationSystem ApplicationSystem { get; set; } // navigation
    public DependencyType DependencyType { get; set; }
    public Server? Server { get; set; }
    public int? ServerId { get; set; }
    public ProviderSubscription? ProviderSubscription { get; set; }
    public int? ProviderSubscriptionId { get; set; }

    public IntegrationService? IntegrationService { get; set; }
    public int? IntegrationServiceId { get; set; }

    public Resource? Resource { get; set; }
    public int? ResourceId { get; set; }
    public virtual float SharePercentage { get; set; }
    public int Year { get; set; } = DateTime.Now.Year;
    public int Month { get; set; } = DateTime.Now.Month;

    public int? SharedServiceId { get; set; }
    public SharedService? SharedService { get; set; } // navigation
}

public class Resource : BaseCostEntity
{
    public string Name { get; set; }
    public string NationalId { get; set; }

}
public class IntegrationService : AuditedAggregateRoot<int>
{
    public string Description { get; set; }
    public string URL { get; set; }
    public  float UnitCost { get; set; }
    public virtual ICollection<CostItem>? CustomCosts { get; set; } = new List<CostItem>();

    public IntegrationSubscriptionType IntegrationSubscriptionType { get; set; }

    public Provider Provider { get; set; }
    public int ProviderId { get; set; }
    public virtual bool Shared { get; set; }

}

public class Portfolio : BaseCostEntity
{
    public string Name { get; set; }

    // Navigation
    public ICollection<ApplicationSystem> ApplicationSystems { get; set; }

}


public class Provider : BaseCostEntity
{
    public string Name { get; set; }

}

public class ProviderSubscription : BaseCostEntity
{

    public string Description { get; set; }
    public string URL { get; set; }
    
    public int ProviderId { get; set; }
    public Provider Provider { get; set; }

}

public class SharedService : BaseCostEntity
{
    public string Description { get; set; }
    public string Name { get; set; }

    public int ProviderId { get; set; }
    public Provider Provider { get; set; }

    public  ICollection<SystemDependency> SystemDependencies { get; set; }
    public int Year { get; set; } = DateTime.Now.Year;
    public int Month { get; set; } = DateTime.Now.Month;
}

public class Server : BaseCostEntity
{
    public string Specification { get; set; }
    public Provider Provider { get; set; }
    public int ProviderId { get; set; }
}

public class ApplicationSystem : BaseCostEntity
{
    public string Name { get; set; }

    // Foreign Key
    public virtual Portfolio Portfolio { get; set; }
    public int PortfolioId { get; set; }

    public virtual ICollection<SystemDependency> SystemDependencies { get; set; }
    //public float TotalYearlyCost =>
    //Dependencies?.Sum(d => d.YearlyCost ?? 0) ?? 0
    //+ Servers?.Sum(s => s.YearlyCost ?? 0) ?? 0
    //+ ProviderSubscriptions?.Sum(p => p.YearlyCost ?? 0) ?? 0
    //+ IntegrationServices?.Sum(i => i.YearlyCost ?? 0) ?? 0
    //+ SystemResources?.Sum(r => r.Resource.YearlyCost ?? 0) ?? 0;

}

public class SystemIntegrationTransaction : AuditedAggregateRoot<int>
{
    public string Description { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public float TotalCost { get; set; }

    public double UsageCount { get; set; }
    public IntegrationService IntegrationService { get; set; }
    public int IntegrationServiceId { get; set; }

    public ApplicationSystem ApplicationSystem { get; set; }
    public int ApplicationSystemId { get; set; }

    public TimlyRequestType RequestType { get; set; }

}
//public class SystemResource : BaseCostEntity
//{
//    public Resource Resource { get; set; }
//    public int ResourceId { get; set; }


//}