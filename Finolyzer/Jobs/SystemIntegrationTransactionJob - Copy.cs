//using Finolyzer.Entities;
//using Hangfire;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using System.Net.Http;
//using System.Text;
//using System.Threading.Tasks;
//using Volo.Abp.DependencyInjection;
//using Volo.Abp.Domain.Repositories;

//namespace Finolyzer.Jobs;

//[Dependency(ServiceLifetime.Transient)]
//public class SystemIntegrationTransactionJob
//{
//    private readonly ILogger<SystemIntegrationTransactionJob> _logger;
//    private readonly IRepository<ApplicationSystem, int> _applicationSystemRepository;
//    private readonly IRepository<ApplicationIntegrationKey, int> _applicationIntegrationKey;
//    private readonly IRepository<SystemIntegrationTransaction, int> _systemIntegrationTransaction;
//    private readonly HttpClient _httpClient;

//    //"ApigeeUrl": "https://apim.thiqah.sa:8080/v1/organizations/thiqah-external/environments/prod/stats/",
//    //    "ApigeeUsername": "analytics@thiqah.sa",
//    //    "ApigeePassword": "AA@integ123"
//    private const string BaseAuthHeader = "Basic YW5hbHl0aWNzQHRoaXFhaC5zYTphbmFseXRpY3NAMTIz";
//    private const string ApigeeUrl = "https://apim.thiqah.sa:8080/v1/organizations/thiqah-external/environments/prod/stats/";


//    public SystemIntegrationTransactionJob(
//        ILogger<SystemIntegrationTransactionJob> logger,
//        IRepository<ApplicationSystem, int> applicationSystemRepository,
//        IRepository<ApplicationIntegrationKey, int> applicationIntegrationKey,
//        IRepository<SystemIntegrationTransaction, int> systemIntegrationTransaction,
//        IHttpClientFactory httpClientFactory)
//    {
//        _logger = logger;
//        _applicationSystemRepository = applicationSystemRepository;
//        _applicationIntegrationKey = applicationIntegrationKey;
//        _systemIntegrationTransaction = systemIntegrationTransaction;
//        _httpClient = httpClientFactory.CreateClient();
//    }

//    public async Task RunAsync(ApigeeAnalyticsJobArgs args)
//    {
//        _logger.LogInformation("Starting SystemIntegrationTransactionJob for Service={Service}, Date={Date}",
//            args.ServiceName, args.ReportDate);

//        // Ensure we don’t duplicate transactions
//        var exists = await _systemIntegrationTransaction.GetListAsync(x =>
//            x.ApplicationIntegrationKeyId == 1 &&
//            x.IntegrationService.SystemMappingKey == args.ServiceName &&
//            x.Year == args.ReportDate.Year &&
//            x.Month == args.ReportDate.Month
//        , true);

//        if (exists.Count > 0)
//        {
//            _logger.LogInformation("Transactions already exist for Service={Service}, Year={Year}, Month={Month}",
//                args.ServiceName, args.ReportDate.Year, args.ReportDate.Month);
//            return;
//        }
//        DateTime fromDate;
//        DateTime toDate;

//        switch (args.TimlyRequestType)
//        {
//            case TimlyRequestType.Monthly:
//                fromDate = new DateTime(args.ReportDate.Year, args.ReportDate.Month, 1, 0, 0, 0);
//                toDate = fromDate.AddMonths(1).AddTicks(-1);  // Last tick of the current month
//                break;

//            case TimlyRequestType.Daily:
//                fromDate = args.ReportDate.Date;  // Start at 00:00:00
//                toDate = fromDate.AddDays(1).AddTicks(-1);  // End at 23:59:59.9999999 (last tick of the day)
//                break;

//            case TimlyRequestType.Yearly:
//                fromDate = new DateTime(args.ReportDate.Year, 1, 1, 0, 0, 0);  // Start of the year
//                toDate = fromDate.AddYears(1).AddTicks(-1);  // End of the year (last tick of the last day)
//                break;

//            default:
//                // If no valid request type, set default to daily
//                fromDate = args.ReportDate.Date;
//                toDate = fromDate.AddDays(1).AddTicks(-1);
//                break;
//        }



//        _httpClient.Timeout = TimeSpan.FromHours(1);

//        var requestUriBuilder = new StringBuilder();
//        requestUriBuilder.Append("developer_email,apiproxy?");
//        requestUriBuilder.Append($"filter=(response_status_code+in+%27200%27,+%27400%27,+%27404%27)");
//        requestUriBuilder.Append($"and+(apiproxy+eq+%27{args.ServiceName}%27)");
//        requestUriBuilder.Append($"and+(client_id+eq+%27{args.ApplicationKey}%27)");
//        requestUriBuilder.Append("&select=sum(message_count)");
//        requestUriBuilder.Append("&limit=1&offset=0");
//        requestUriBuilder.Append($"&timeRange={fromDate:MM'%2F'dd'%2F'yyyy'+'00':'00':'00}~{toDate:MM'%2F'dd'%2F'yyyy'+'23':'59':'59}&tzo=180");

//        var requestUri = requestUriBuilder.ToString();

//        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApigeeUrl}{requestUri}");
//        request.Headers.Add("Authorization", BaseAuthHeader);

//        try
//        {
//            var response = await _httpClient.SendAsync(request);
//            response.EnsureSuccessStatusCode();

//            var rawJson = await response.Content.ReadAsStringAsync();

//            // Dynamic parse since we only need the total transaction count
//            var json = JObject.Parse(rawJson);

//            // Navigate safely to the metric value in the Apigee analytics response
//            var totalTransactions = json["environments"]?
//                .FirstOrDefault()?["metrics"]?
//                .FirstOrDefault()?["values"]?
//                .FirstOrDefault()?.Value<long>() ?? 0;

//            _logger.LogInformation("Total transactions for Service={Service}: {Count}", args.ServiceName, totalTransactions);

//            return totalTransactions;
//        }
//        catch (TimeoutException ex)
//        {
//            _logger.LogError(ex, "Apigee request timed out for Service={Service}", args.ServiceName);
//            throw;
//        }
//        catch (HttpRequestException ex)
//        {
//            _logger.LogError(ex, "HTTP request failed for Service={Service}", args.ServiceName);
//            throw;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Unexpected error while fetching transactions for Service={Service}", args.ServiceName);
//            throw;
//        }


//        _logger.LogInformation("Completed SystemIntegrationTransactionJob for Service={Service}", args.ServiceName);
//    }

//    private List<AnalyticReportV2> MapAnalyticsToEntities(AnalyticsReport report)
//    {
//        var entities = new List<AnalyticReportV2>();

//        foreach (var dimension in report.Environments[0].Dimensions)
//        {
//            if (dimension.Metrics == null || dimension.Metrics.Length == 0) continue;

//            var values = dimension.Name.Split(',');
//            if (values.Length < 6) continue;

//            entities.Add(new AnalyticReportV2
//            {
//                DeveloperEmail = values[0],
//                ProductName = values[1],
//                ProxyName = values[2],
//                ServiceName = values[3],
//                StatusCode = values[4],
//                ServicesType = values[5],
//                Count = double.TryParse(dimension.Metrics[0].Values.FirstOrDefault(), out var count) ? count : 0
//            });
//        }

//        return entities;
//    }
//}

//// DTO Models
//public class AnalyticsReport
//{
//    [JsonProperty("environments")]
//    public Environment[] Environments { get; set; }

//    [JsonProperty("metaData")]
//    public MetaData MetaData { get; set; }
//}

//public class Environment
//{
//    [JsonProperty("dimensions")]
//    public Dimension[] Dimensions { get; set; }
//}

//public class Dimension
//{
//    [JsonProperty("metrics")]
//    public Metric[] Metrics { get; set; }

//    [JsonProperty("name")]
//    public string Name { get; set; }
//}

//public class Metric
//{
//    [JsonProperty("values")]
//    public string[] Values { get; set; }
//}

//public class MetaData
//{
//    [JsonProperty("errors")]
//    public object[] Errors { get; set; }

//    [JsonProperty("notices")]
//    public string[] Notices { get; set; }
//}

//public class AnalyticReportV2
//{
//    public int Id { get; set; }
//    public int AnalyticRequestId { get; set; }
//    public string DeveloperEmail { get; set; }
//    public string ProductName { get; set; }
//    public string ProxyName { get; set; }
//    public string ServiceName { get; set; }
//    public string StatusCode { get; set; }
//    public double? Count { get; set; }
//    public string ServicesType { get; set; }
//}
