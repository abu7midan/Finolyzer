

namespace Finolyzer.Jobs;
public class ApigeeAnalyticsJobArgs
{
    public string ApplicationName { get; set; }
    public string ApplicationKey { get; set; }
    public string ServiceName { get; set; }
    public string RecurringJobId { get; set; }
    public string CronExpression { get; set; }
    public DateTime ReportDate { get; set; }
}