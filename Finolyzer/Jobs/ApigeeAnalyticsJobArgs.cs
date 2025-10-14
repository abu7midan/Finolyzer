

using Finolyzer.Entities;

namespace Finolyzer.Jobs;
public class ApigeeAnalyticsJobArgs
{
    public int ApplicationSystemId { get; set; }
    public string ApplicationName { get; set; }
    public int ApplicationIntegrationKeyId { get; set; }
    public string ApplicationKey { get; set; }
    public int IntegrationServiceId { get; set; }
    public string IntegrationServiceName { get; set; }
    public string RecurringJobId { get; set; }
    public string CronExpression { get; set; }
    public DateTime ReportDate { get; set; }
    public TimlyRequestType TimlyRequestType { get; set; }

}