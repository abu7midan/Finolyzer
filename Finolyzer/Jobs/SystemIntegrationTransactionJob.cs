using Finolyzer.Entities;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Finolyzer.Jobs
{
    [Dependency(ServiceLifetime.Transient)]
    public class SystemIntegrationTransactionJob
    {
        private readonly ILogger<SystemIntegrationTransactionJob> _logger;
        private readonly IRepository<SystemIntegrationTransaction, int> _systemIntegrationTransaction;
        private readonly HttpClient _httpClient;

        private const string BaseAuthHeader = "Basic YW5hbHl0aWNzQHRoaXFhaC5zYTphbmFseXRpY3NAMTIz"; // Basic Auth header
        private const string ApigeeUrl = "https://apim.thiqah.sa:8080/v1/organizations/thiqah-external/environments/prod/stats/";

        public SystemIntegrationTransactionJob(
            ILogger<SystemIntegrationTransactionJob> logger,
            IRepository<SystemIntegrationTransaction, int> systemIntegrationTransaction,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _systemIntegrationTransaction = systemIntegrationTransaction;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task RunAsync(ApigeeAnalyticsJobArgs args)
        {
            _logger.LogInformation("Starting SystemIntegrationTransactionJob for Service={Service}, Date={Date}",
                args.IntegrationServiceName, args.ReportDate);

            // Prevent duplicates
            var exists = await _systemIntegrationTransaction.GetListAsync(
                x => x.ApplicationIntegrationKeyId == 1 &&
                     x.IntegrationService.SystemMappingKey == args.IntegrationServiceName &&
                     x.Year == args.ReportDate.Year &&
                     x.Month == args.ReportDate.Month,
                includeDetails: true);

            if (exists.Count > 0)
            {
                _logger.LogInformation("Transactions already exist for Service={Service}, Year={Year}, Month={Month}",
                    args.IntegrationServiceName, args.ReportDate.Year, args.ReportDate.Month);
                return;
            }

            DateTime fromDate;
            DateTime toDate;

            // Handle date ranges based on the request type
            (fromDate, toDate) = args.TimlyRequestType switch
            {
                TimlyRequestType.Monthly => (
                    new DateTime(args.ReportDate.Year, args.ReportDate.Month, 1),
                    new DateTime(args.ReportDate.Year, args.ReportDate.Month, 1).AddMonths(1).AddTicks(-1)
                ),
                TimlyRequestType.Daily => (
                    args.ReportDate.Date,
                    args.ReportDate.Date.AddDays(1).AddTicks(-1)
                ),
                TimlyRequestType.Yearly => (
                    new DateTime(args.ReportDate.Year, 1, 1),
                    new DateTime(args.ReportDate.Year, 1, 1).AddYears(1).AddTicks(-1)
                ),
                _ => (args.ReportDate.Date, args.ReportDate.Date.AddDays(1).AddTicks(-1))
            };

            // Convert the fromDate and toDate to the required time range format (MM/dd/yyyy HH:mm:ss)
            string fromDateStr = fromDate.ToString("MM/dd/yyyy HH:mm:ss");
            string toDateStr = toDate.ToString("MM/dd/yyyy HH:mm:ss");

            // Construct the request URI (encoding the parameters correctly)
            var requestUriBuilder = new StringBuilder();
            requestUriBuilder.Append("developer_email,api_product,apiproxy?");
            requestUriBuilder.Append($"filter=(response_status_code+in+%27200%27,+%27400%27,+%27404%27)");
            requestUriBuilder.Append($"and+(apiproxy+eq+%27{args.IntegrationServiceName}%27)");
            requestUriBuilder.Append($"and+(client_id+eq+%27{args.ApplicationKey}%27)");
            requestUriBuilder.Append("&select=sum(message_count)");
            requestUriBuilder.Append("&limit=1&offset=0");
            requestUriBuilder.Append($"&timeRange={Uri.EscapeDataString(fromDateStr)}~{Uri.EscapeDataString(toDateStr)}");

            var requestUri = $"{ApigeeUrl}{requestUriBuilder}";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Authorization", BaseAuthHeader);

            try
            {
                _httpClient.Timeout = TimeSpan.FromHours(1);

                // Send the request
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var rawJson = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(rawJson);

                // Extract the total transaction count
                var totalTransactions = json["environments"]?
                    .FirstOrDefault()?
                    .SelectToken("dimensions[0].metrics[0].values[0]")?
                    .Value<string>();

                if (double.TryParse(totalTransactions, out double transactionCount))
                {
                    _logger.LogInformation("Total transactions for Service={Service}: {Count}", args.IntegrationServiceName, transactionCount);

                    if (transactionCount > 0)
                    {
                        // Save transaction summary (example)
                        var record = new SystemIntegrationTransaction
                        {
                            ApplicationSystemId= args.ApplicationSystemId,
                            RequestType= args.TimlyRequestType,
                            Description =$"Usage Count for Service {args.IntegrationServiceName} for app {args.ApplicationName} reuest is {args.TimlyRequestType.ToString()} from date {fromDateStr} to date {toDateStr} reposrt date {args.ReportDate}",
                            ApplicationIntegrationKeyId = args.ApplicationIntegrationKeyId,
                            IntegrationServiceId=args.IntegrationServiceId,
                            Year = args.ReportDate.Year,
                            Month = args.ReportDate.Month,
                            Day = args.ReportDate.Day,
                            UsageCount = transactionCount,
                            CreationTime = DateTime.UtcNow
                        };

                        await _systemIntegrationTransaction.InsertAsync(record, autoSave: true);
                        _logger.LogInformation("Saved total transactions ({Count}) for {Service}", transactionCount, args.IntegrationServiceName);
                    }
                    else
                    {
                        _logger.LogWarning("No transactions found for Service={Service} during {From} - {To}", args.IntegrationServiceName, fromDate, toDate);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to parse transaction count from response for Service={Service}. Raw response: {Response}",
                        args.IntegrationServiceName, rawJson);
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Apigee request timed out for Service={Service}", args.IntegrationServiceName);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while fetching transactions for Service={Service}", args.IntegrationServiceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing analytics for Service={Service}", args.IntegrationServiceName);
                throw;
            }

            _logger.LogInformation("Completed SystemIntegrationTransactionJob for Service={Service}", args.IntegrationServiceName);
        }
    }
}
