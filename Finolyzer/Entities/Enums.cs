namespace Finolyzer.Entities;

public enum TimlyRequestType
{
    Daily = 0,
    Monthly = 1,
    Quarter = 2,
    Yearly = 3,
}
public enum DependencyType
{
    Server = 1,
    ProviderSubscription = 2,
    IntegrationService = 3,
    Resource = 4
}
public enum IntegrationSubscriptionType
{
    Free = 0,
    Paid = 1,
    Internal = 2,
}