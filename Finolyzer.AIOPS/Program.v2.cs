//// Program.cs
//// .NET 9 Minimal API + ML.NET (PCA anomaly + KMeans clustering + trend guard)
//// LIMIT READS: fetch only 50 docs per index (no scroll), save to NDJSON, train from files, suggest from files
//// Verbose console logging.
////
//// Endpoints:
////   GET  /health
////   POST /train            -> fetch 50 docs (metrics + apm tx), write NDJSON, train
////   GET  /suggestions      -> load models, rebuild features from NDJSON, return suggestions
////
//// Env vars (defaults from your earlier code):
////   ES_URL
////   ES_API_KEY_BASE64
////   MB_INDEX (metrics indices)
////   APM_TX_INDEX
////
//// NuGet:
////   Microsoft.ML
////   Microsoft.ML.Mkl.Components (optional)

//using Microsoft.ML;
//using Microsoft.ML.Data;
//using Microsoft.ML.Transforms;
//using System.Globalization;
//using System.Net.Http.Headers;
//using System.Text;
//using System.Text.Json;

//namespace CostAdvisor;

//// -------------------- DTOs --------------------

//public sealed class HostFeatures
//{
//    public string Host { get; set; } = "";
//    public float MemP95GiB { get; set; }
//    public float CpuP95 { get; set; }             // 0..1
//    public float NetInP95Kbps { get; set; }
//    public float NetOutP95Kbps { get; set; }
//    public float RpmP95 { get; set; }
//    public float MemHeadroomGiB { get; set; }     // alloc - p95
//    public float CpuHeadroom { get; set; }        // 1 - cpu_p95
//    public float IdleFraction { get; set; }       // docs with cpu<5% / total docs
//    public float OffhoursIdleFraction { get; set; }
//    public float DataCompleteness { get; set; }   // non-empty hours / 720
//    public float AllocMemGiB { get; set; }
//}

//public sealed class AnomalyOutput
//{
//    public bool PredictedLabel { get; set; }
//    public float Score { get; set; }
//    public float PValue { get; set; }
//}

//public sealed class ClusterOutput
//{
//    [ColumnName("PredictedLabel")]
//    public uint ClusterId { get; set; }
//    public float[]? Distance { get; set; }
//}

//public static class Columns
//{
//    public static readonly string[] AnomalyCols = new[]
//    {
//        nameof(HostFeatures.MemHeadroomGiB),
//        nameof(HostFeatures.CpuHeadroom),
//        nameof(HostFeatures.IdleFraction),
//        nameof(HostFeatures.OffhoursIdleFraction),
//        nameof(HostFeatures.RpmP95),
//        nameof(HostFeatures.NetInP95Kbps),
//        nameof(HostFeatures.NetOutP95Kbps)
//    };

//    public static readonly string[] ClusterCols = new[]
//    {
//        nameof(HostFeatures.CpuP95),
//        nameof(HostFeatures.MemP95GiB),
//        nameof(HostFeatures.RpmP95),
//        nameof(HostFeatures.NetInP95Kbps),
//        nameof(HostFeatures.NetOutP95Kbps)
//    };
//}

//// -------------------- ML Trainer --------------------

//public static class ModelTrainer
//{
//    public static (ITransformer anomalyModel, ITransformer clusterModel) Train(MLContext ml, List<HostFeatures> rows, string modelDir, ILogger logger)
//    {
//        logger.LogInformation("ModelTrainer: Loading {RowCount} rows into IDataView...", rows.Count);
//        var data = ml.Data.LoadFromEnumerable(rows);

//        var anomalyPairs = Columns.AnomalyCols.Select(c => new InputOutputColumnPair(c, c)).ToArray();
//        var clusterPairs = Columns.ClusterCols.Select(c => new InputOutputColumnPair(c, c)).ToArray();

//        logger.LogInformation("ModelTrainer: Building anomaly pipeline (RandomizedPCA)...");
//        var anomalyPipeline =
//            ml.Transforms.ReplaceMissingValues(anomalyPairs, MissingValueReplacingEstimator.ReplacementMode.Mean)
//              .Append(ml.Transforms.NormalizeMinMax(anomalyPairs))
//              .Append(ml.Transforms.Concatenate("Features", Columns.AnomalyCols))
//              .Append(ml.AnomalyDetection.Trainers.RandomizedPca(
//                  featureColumnName: "Features",
//                  rank: 5,
//                  ensureZeroMean: true,
//                  seed: 42));

//        logger.LogInformation("ModelTrainer: Fitting anomaly model...");
//        var anomalyModel = anomalyPipeline.Fit(data);

//        logger.LogInformation("ModelTrainer: Building cluster pipeline (KMeans, k=4)...");
//        var clusterPipeline =
//            ml.Transforms.ReplaceMissingValues(clusterPairs, MissingValueReplacingEstimator.ReplacementMode.Mean)
//              .Append(ml.Transforms.NormalizeMinMax(clusterPairs))
//              .Append(ml.Transforms.Concatenate("Features", Columns.ClusterCols))
//              .Append(ml.Clustering.Trainers.KMeans(featureColumnName: "Features", numberOfClusters: 4));

//        logger.LogInformation("ModelTrainer: Fitting cluster model...");
//        var clusterModel = clusterPipeline.Fit(data);

//        Directory.CreateDirectory(modelDir);
//        var anomalyPath = Path.Combine(modelDir, "anomaly.zip");
//        var clusterPath = Path.Combine(modelDir, "cluster.zip");

//        logger.LogInformation("ModelTrainer: Saving models -> {AnomalyPath}, {ClusterPath}", anomalyPath, clusterPath);
//        ml.Model.Save(anomalyModel, data.Schema, anomalyPath);
//        ml.Model.Save(clusterModel, data.Schema, clusterPath);

//        logger.LogInformation("ModelTrainer: Done.");
//        return (anomalyModel, clusterModel);
//    }
//}

//// -------------------- Trend guard --------------------

//public static class TrendGuard
//{
//    public static bool IsTrendingUp(float[] cpuSeries, int minPoints = 24 * 14, double slopeThreshold = 0.0005)
//    {
//        if (cpuSeries == null || cpuSeries.Length < minPoints) return false;
//        int n = cpuSeries.Length;
//        double meanX = (n - 1) / 2.0;
//        double meanY = cpuSeries.Average();
//        double num = 0, den = 0;
//        for (int i = 0; i < n; i++)
//        {
//            double dx = i - meanX;
//            double dy = cpuSeries[i] - meanY;
//            num += dx * dy;
//            den += dx * dx;
//        }
//        double slope = den == 0 ? 0 : num / den; // per-hour change in CPU (0..1)
//        return slope > slopeThreshold;
//    }
//}

//// -------------------- Streaming percentile (P²) --------------------

//public sealed class P2Quantile
//{
//    private readonly double q;
//    private readonly double[] x = new double[5];
//    private readonly double[] n = new double[5];
//    private readonly double[] np = new double[5];
//    private readonly double[] dn = new double[5];
//    private int count;

//    public P2Quantile(double quantile)
//    {
//        q = quantile;
//        dn[0] = 0; dn[1] = q / 2; dn[2] = q; dn[3] = (1 + q) / 2; dn[4] = 1;
//    }

//    public void Add(double v)
//    {
//        if (double.IsNaN(v)) return;

//        if (count < 5)
//        {
//            x[count++] = v;
//            if (count == 5)
//            {
//                Array.Sort(x);
//                n[0] = 1; n[1] = 2; n[2] = 3; n[3] = 4; n[4] = 5;
//                np[0] = 1; np[1] = 1 + 2 * q; np[2] = 1 + 4 * q; np[3] = 3 + 2 * q; np[4] = 5;
//            }
//            return;
//        }

//        int k = v < x[0] ? 0 : v >= x[4] ? 3 : Array.FindLastIndex(x, xi => v >= xi);
//        if (v < x[0]) x[0] = v;
//        else if (v >= x[4]) x[4] = v;

//        for (int i = k + 1; i < 5; i++) n[i] += 1;
//        for (int i = 0; i < 5; i++) np[i] += dn[i];

//        for (int i = 1; i < 4; i++)
//        {
//            double d = np[i] - n[i];
//            if ((d >= 1 && n[i + 1] - n[i] > 1) || (d <= -1 && n[i - 1] - n[i] < -1))
//            {
//                int s = Math.Sign(d);
//                double newx = x[i] + s * ((n[i] - n[i - 1] + s) * (x[i + 1] - x[i]) / (n[i + 1] - n[i]) +
//                                          (n[i + 1] - n[i] - s) * (x[i] - x[i - 1]) / (n[i] - n[i - 1])) / (n[i + 1] - n[i - 1]);
//                if (newx > x[i - 1] && newx < x[i + 1]) x[i] = newx;
//                else x[i] += s * (x[i + s] - x[i]) / (n[i + s] - n[i]);
//                n[i] += s;
//            }
//        }
//        count++;
//    }

//    public double Value
//    {
//        get
//        {
//            if (count == 0) return double.NaN;
//            if (count < 5)
//            {
//                var sorted = x.Take(count).OrderBy(v => v).ToArray();
//                var idx = (int)Math.Floor((count - 1) * q);
//                return sorted[idx];
//            }
//            return x[2];
//        }
//    }
//}

//// -------------------- API --------------------

//public class Program
//{
//    // ======= Config (defaults preserved) =======
//    internal static string EsUrl => Environment.GetEnvironmentVariable("ES_URL") ?? "https://ELK.THIQAH.SA";
//    internal static string EsApiKeyBase64 => Environment.GetEnvironmentVariable("ES_API_KEY_BASE64") ?? "enhTTEJwb0JTRmJRbGFOT0RVdmY6YlZmd1BwamZTQWVVTTQ2T2Nlek94Zw==";
//    internal static string MetricsIndex => Environment.GetEnvironmentVariable("MB_INDEX") ?? ".ds-metricbeat-*,metricbeat-*,metricbeat-psintg-*,metricbeat-rabbitmq-*,metrics-*-elk,*metricbeat*,*metric*";
//    internal static string ApmTxIndex => Environment.GetEnvironmentVariable("APM_TX_INDEX") ?? "apm-*";

//    // Export destination
//    internal static string MetricsOutDir => "./data/metrics";
//    internal static string ApmOutDir => "./data/apm";

//    public static async Task Main(string[] args)
//    {
//        var builder = WebApplication.CreateBuilder(args);
//        builder.Logging.ClearProviders();
//        builder.Logging.AddConsole();
//        builder.Logging.SetMinimumLevel(LogLevel.Information);

//        var app = builder.Build();
//        var logger = app.Logger;

//        app.MapGet("/health", () =>
//        {
//            logger.LogInformation("GET /health at {NowUtc}", DateTime.UtcNow);
//            return Results.Ok(new { ok = true, ts = DateTime.UtcNow });
//        });

//        // ======== /train: fetch ONLY 50 docs from each index (last 30d), write NDJSON, then train ========
//        app.MapPost("/train", async () =>
//        {
//            try
//            {
//                var toUtc = DateTime.UtcNow;
//                var fromUtc = toUtc.AddDays(-30);
//                logger.LogInformation("POST /train: Fetching limited docs (size=50) for window {From} -> {To}", fromUtc, toUtc);

//                Directory.CreateDirectory(MetricsOutDir);
//                Directory.CreateDirectory(ApmOutDir);

//                var metricsFile = Path.Combine(MetricsOutDir, $"limited_{toUtc:yyyyMMdd_HHmmss}_metrics.ndjson");
//                var apmFile = Path.Combine(ApmOutDir, $"limited_{toUtc:yyyyMMdd_HHmmss}_apm.ndjson");

//                await ExportLimitedNdjson(
//                    MetricsIndex,
//                    fromUtc,
//                    toUtc,
//                    metricsFile,
//                    BuildMetricsQueryBodyLimited,
//                    300,
//                    logger);

//                await ExportLimitedNdjson(
//                    ApmTxIndex,
//                    fromUtc,
//                    toUtc,
//                    apmFile,
//                    BuildApmTxQueryBodyLimited,
//                    300,
//                    logger);

//                logger.LogInformation("POST /train: Building features from files...");
//                var ml = new MLContext(seed: 42);
//                var features = await BuildFeaturesFromFiles(MetricsOutDir, ApmOutDir, logger);
//                logger.LogInformation("POST /train: Got {Count} hosts with features.", features.Count);

//                if (features.Count == 0)
//                {
//                    logger.LogWarning("POST /train: No features were built. Training aborted.");
//                    return Results.Ok(new { trained = 0, note = "No features from limited files." });
//                }

//                logger.LogInformation("POST /train: Training models...");
//                var (anomaly, cluster) = ModelTrainer.Train(ml, features, "models", logger);
//                logger.LogInformation("POST /train: Training done.");
//                return Results.Ok(new { trained = features.Count, when = DateTime.UtcNow, metricsFile, apmFile });
//            }
//            catch (Exception ex)
//            {
//                LogException(logger, "POST /train failed", ex);
//                return Results.Problem(ex.Message);
//            }
//        });

//        // ======== /suggestions: build features from limited files, load models, score ========
//        app.MapGet("/suggestions", async () =>
//        {
//            try
//            {
//                logger.LogInformation("GET /suggestions: Loading features from files...");
//                var ml = new MLContext();

//                var features = await BuildFeaturesFromFiles(MetricsOutDir, ApmOutDir, logger);
//                logger.LogInformation("GET /suggestions: Loaded features for {Count} hosts.", features.Count);

//                if (features.Count == 0)
//                {
//                    logger.LogWarning("GET /suggestions: No features found. Did you run /train?");
//                    return Results.Ok(new { generated_at = DateTime.UtcNow, results = Array.Empty<object>(), note = "No features from files. Call /train first." });
//                }

//                var anomalyPath = Path.Combine("models", "anomaly.zip");
//                var clusterPath = Path.Combine("models", "cluster.zip");
//                if (!File.Exists(anomalyPath) || !File.Exists(clusterPath))
//                {
//                    logger.LogError("GET /suggestions: Models not found: {A}, {C}", anomalyPath, clusterPath);
//                    return Results.Problem("Models not found. Call /train first.");
//                }

//                logger.LogInformation("GET /suggestions: Loading models...");
//                using var fsA = File.OpenRead(anomalyPath);
//                using var fsC = File.OpenRead(clusterPath);
//                var anomalyModel = ml.Model.Load(fsA, out _);
//                var clusterModel = ml.Model.Load(fsC, out _);

//                var anomalyEngine = ml.Model.CreatePredictionEngine<HostFeatures, AnomalyOutput>(anomalyModel);
//                var clusterEngine = ml.Model.CreatePredictionEngine<HostFeatures, ClusterOutput>(clusterModel);

//                var results = new List<object>();

//                foreach (var h in features)
//                {
//                    try
//                    {
//                        var ao = anomalyEngine.Predict(h);
//                        var co = clusterEngine.Predict(h);

//                        var cpuSeries = await GetHourlyCpuSeriesForHostFromFiles(MetricsOutDir, h.Host, 30, logger);
//                        var trendingUp = TrendGuard.IsTrendingUp(cpuSeries);
//                        var suggs = new List<object>();

//                        if ((IsIdleCluster(co.ClusterId) || ao.Score >= 0.7f) && !trendingUp && h.DataCompleteness >= 0.9f)
//                        {
//                            var targetGiB = Math.Max(2, (int)Math.Ceiling(h.MemP95GiB * 1.25));
//                            suggs.Add(new
//                            {
//                                type = "rightsize_memory",
//                                message = $"{h.Host}: model flags under-utilization; set RAM ≈ {targetGiB} GiB (p95 {h.MemP95GiB:0.0} GiB).",
//                                confidence = Confidence(h, ao, trendingUp)
//                            });

//                            var targetCpuTier = MapCpuToTier(h.CpuP95);
//                            suggs.Add(new
//                            {
//                                type = "rightsize_cpu",
//                                message = $"{h.Host}: p95 CPU {h.CpuP95 * 100:0.#}% → suggest tier {targetCpuTier}.",
//                                confidence = Confidence(h, ao, trendingUp)
//                            });
//                        }

//                        if (h.OffhoursIdleFraction >= 0.8f && !trendingUp)
//                        {
//                            suggs.Add(new
//                            {
//                                type = "offhours_shutdown",
//                                message = $"{h.Host}: off-hours idle {h.OffhoursIdleFraction:P0} → schedule stop 00:00–07:00.",
//                                confidence = 0.65
//                            });
//                        }

//                        if (h.RpmP95 < 1 && h.CpuP95 < 0.05f && (h.NetInP95Kbps + h.NetOutP95Kbps) < 10)
//                        {
//                            suggs.Add(new
//                            {
//                                type = "shutdown_candidate",
//                                message = $"{h.Host}: near-zero traffic and CPU; candidate to turn off / consolidate.",
//                                confidence = 0.7
//                            });
//                        }

//                        if (suggs.Count > 0)
//                        {
//                            results.Add(new { host = h.Host, suggestions = suggs });
//                            logger.LogInformation("GET /suggestions: Host={Host} -> {Count} suggestions", h.Host, suggs.Count);
//                        }
//                        else
//                        {
//                            logger.LogInformation("GET /suggestions: Host={Host} -> no suggestions", h.Host);
//                        }
//                    }
//                    catch (Exception exHost)
//                    {
//                        LogException(logger, $"Scoring/suggesting for host '{h.Host}' failed", exHost);
//                    }
//                }

//                logger.LogInformation("GET /suggestions: Total hosts with suggestions: {Count}", results.Count);
//                return Results.Json(new { generated_at = DateTime.UtcNow, results });
//            }
//            catch (Exception ex)
//            {
//                LogException(logger, "GET /suggestions failed", ex);
//                return Results.Problem(ex.Message);
//            }
//        });

//        await app.RunAsync();
//    }

//    // -------------------- Policy helpers --------------------

//    private static double Confidence(HostFeatures h, AnomalyOutput ao, bool trendingUp)
//    {
//        var baseConf = 0.5;
//        baseConf += Math.Clamp(h.DataCompleteness, 0, 1) * 0.2;
//        baseConf += Math.Clamp(ao.Score, 0, 1) * 0.2;
//        if (trendingUp) baseConf -= 0.3;
//        baseConf = Math.Clamp(baseConf, 0.1, 0.95);
//        return Math.Round(baseConf, 2);
//    }

//    private static bool IsIdleCluster(uint clusterId) => clusterId == 1;

//    private static string MapCpuToTier(float cpuP95) =>
//        cpuP95 switch
//        {
//            < 0.10f => "XS (1 vCPU)",
//            < 0.20f => "S (2 vCPU)",
//            < 0.35f => "M (4 vCPU)",
//            < 0.60f => "L (8 vCPU)",
//            _ => "Keep current"
//        };

//    private static void LogException(ILogger logger, string prefix, Exception ex)
//    {
//        logger.LogError(ex, "{Prefix}: {Message}", prefix, ex.Message);
//    }

//    // -------------------- Elasticsearch: LIMITED fetch (size=50, no scroll) --------------------

//    private static HttpClient CreateEsHttp(ILogger logger)
//    {
//        logger.LogInformation("Creating HttpClient for ES: {Url}", EsUrl);
//        var http = new HttpClient { BaseAddress = new Uri(EsUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(3) };
//        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", EsApiKeyBase64);
//        return http;
//    }

//    private static async Task ExportLimitedNdjson(
//        string indexPattern,
//        DateTime fromUtc,
//        DateTime toUtc,
//        string outFile,
//        Func<DateTime, DateTime, int, string> buildLimitedBody,
//        int size,
//        ILogger logger)
//    {
//        try
//        {
//            Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
//            var body = buildLimitedBody(fromUtc, toUtc, size);
//            using var http = CreateEsHttp(logger);

//            var url = $"{indexPattern}/_search";
//            logger.LogInformation("ExportLimited: POST {Url} size={Size} range={From:o}..{To:o} -> {File}", url, size, fromUtc, toUtc, outFile);

//            using var req = new HttpRequestMessage(HttpMethod.Post, url)
//            {
//                Content = new StringContent(body, Encoding.UTF8, "application/json")
//            };
//            using var res = await http.SendAsync(req);
//            res.EnsureSuccessStatusCode();
//            using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());

//            int lines = 0;
//            await using var fs = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
//            using var writer = new StreamWriter(fs, new UTF8Encoding(false));

//            if (doc.RootElement.TryGetProperty("hits", out var hits) &&
//                hits.TryGetProperty("hits", out var arr) &&
//                arr.ValueKind == JsonValueKind.Array)
//            {
//                foreach (var h in arr.EnumerateArray())
//                {
//                    if (h.TryGetProperty("_source", out var src))
//                    {
//                        await writer.WriteLineAsync(src.GetRawText());
//                        lines++;
//                    }
//                }
//            }
//            logger.LogInformation("ExportLimited: wrote {Lines} lines to {File}", lines, outFile);
//        }
//        catch (Exception ex)
//        {
//            LogException(logger, $"ExportLimited failed for index '{indexPattern}'", ex);
//            throw;
//        }
//    }

//    // Pull only fields we use; sort newest first so we get the latest 50 docs
//    private static string BuildMetricsQueryBodyLimited(DateTime gte, DateTime lt, int size)
//    {
//        var gteS = gte.ToString("o", CultureInfo.InvariantCulture);
//        var ltS = lt.ToString("o", CultureInfo.InvariantCulture);
//        return $@"
//{{
//  ""size"": {size},
//  ""_source"": [""@timestamp"", ""host.name"", ""system.memory.total"", ""system.memory.actual.free"",
//               ""system.cpu.total.norm.pct"", ""system.process.cpu.total.norm.pct"",
//               ""system.network.in.bytes"", ""system.network.out.bytes""],
//  ""query"": {{
//    ""bool"": {{
//      ""filter"": [
//        {{ ""range"": {{ ""@timestamp"": {{ ""gte"": ""{gteS}"", ""lt"": ""{ltS}"" }} }} }}
//      ]
//    }}
//  }},
//  ""sort"": [ {{ ""@timestamp"": {{ ""order"": ""desc"" }} }} ]
//}}";
//    }

//    private static string BuildApmTxQueryBodyLimited(DateTime gte, DateTime lt, int size)
//    {
//        var gteS = gte.ToString("o", CultureInfo.InvariantCulture);
//        var ltS = lt.ToString("o", CultureInfo.InvariantCulture);
//        return $@"
//{{
//  ""size"": {size},
//  ""_source"": [""@timestamp"", ""host.name"", ""transaction.id""],
//  ""query"": {{
//    ""bool"": {{
//      ""filter"": [
//        {{ ""term"": {{ ""processor.event"": ""transaction"" }} }},
//        {{ ""range"": {{ ""@timestamp"": {{ ""gte"": ""{gteS}"", ""lt"": ""{ltS}"" }} }} }}
//      ]
//    }}
//  }},
//  ""sort"": [ {{ ""@timestamp"": {{ ""order"": ""desc"" }} }} ]
//}}";
//    }

//    // -------------------- Build features from files (logging) --------------------

//    private sealed class Acc
//    {
//        public P2Quantile MemP95 = new(0.95);
//        public P2Quantile CpuP95 = new(0.95);
//        public P2Quantile NetInP95 = new(0.95);
//        public P2Quantile NetOutP95 = new(0.95);
//        public double MemTotalMaxBytes;
//        public long TotalDocs;
//        public long IdleDocs;
//        public long OffIdleDocs;
//        public HashSet<string> Hours = new(); // yyyyMMddHH
//    }

//    private static async Task<List<HostFeatures>> BuildFeaturesFromFiles(string metricsDir, string? apmDir, ILogger logger)
//    {
//        var acc = new Dictionary<string, Acc>(StringComparer.OrdinalIgnoreCase);

//        // METRICS
//        if (Directory.Exists(metricsDir))
//        {
//            logger.LogInformation("Features: Reading metrics files from {Dir}", metricsDir);
//            foreach (var file in Directory.EnumerateFiles(metricsDir, "*.ndjson", SearchOption.TopDirectoryOnly))
//            {
//                int fileLines = 0;
//                try
//                {
//                    await foreach (var doc in ReadNdjson(file))
//                    {
//                        fileLines++;
//                        if (!doc.TryGetProperty("@timestamp", out var tsProp)) continue;
//                        var ts = tsProp.GetDateTime().ToUniversalTime();

//                        string host = "";
//                        if (doc.TryGetProperty("host", out var hostObj) && hostObj.TryGetProperty("name", out var nameProp))
//                            host = nameProp.GetString() ?? "";
//                        if (string.IsNullOrEmpty(host)) continue;

//                        var hourKey = ts.ToString("yyyyMMddHH");
//                        if (!acc.TryGetValue(host, out var a))
//                            acc[host] = a = new Acc();

//                        a.Hours.Add(hourKey);
//                        a.TotalDocs++;

//                        // mem used = total - actual.free
//                        var memTotal = TryNum(doc, "system.memory.total");
//                        var memFree = TryNum(doc, "system.memory.actual.free");
//                        if (!double.IsNaN(memTotal) && !double.IsNaN(memFree))
//                        {
//                            a.MemP95.Add(memTotal - memFree);
//                            if (memTotal > a.MemTotalMaxBytes) a.MemTotalMaxBytes = memTotal;
//                        }

//                        // cpu
//                        var cpu = TryNum(doc, "system.cpu.total.norm.pct");
//                        if (double.IsNaN(cpu)) cpu = TryNum(doc, "system.process.cpu.total.norm.pct");
//                        if (!double.IsNaN(cpu))
//                        {
//                            a.CpuP95.Add(cpu);
//                            if (cpu < 0.05) a.IdleDocs++;
//                            if (ts.Hour < 7 && cpu < 0.05) a.OffIdleDocs++;
//                        }

//                        // net
//                        var nin = TryNum(doc, "system.network.in.bytes");
//                        var nout = TryNum(doc, "system.network.out.bytes");
//                        if (!double.IsNaN(nin)) a.NetInP95.Add(nin);
//                        if (!double.IsNaN(nout)) a.NetOutP95.Add(nout);
//                    }
//                    logger.LogInformation("Features: Parsed {Lines} lines from {File}", fileLines, file);
//                }
//                catch (Exception exFile)
//                {
//                    LogException(logger, $"Parsing metrics file '{file}' failed", exFile);
//                }
//            }
//        }
//        else
//        {
//            logger.LogWarning("Features: Metrics directory not found: {Dir}", metricsDir);
//        }

//        // APM TX → RPM p95
//        var rpmP95 = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
//        if (!string.IsNullOrWhiteSpace(apmDir) && Directory.Exists(apmDir))
//        {
//            logger.LogInformation("Features: Reading APM tx files from {Dir}", apmDir);
//            var perHostMinute = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

//            foreach (var file in Directory.EnumerateFiles(apmDir, "*.ndjson", SearchOption.TopDirectoryOnly))
//            {
//                int fileLines = 0;
//                try
//                {
//                    await foreach (var doc in ReadNdjson(file))
//                    {
//                        fileLines++;

//                        if (!doc.TryGetProperty("@timestamp", out var tsProp)) continue;
//                        var ts = tsProp.GetDateTime().ToUniversalTime();

//                        string host = "";
//                        if (doc.TryGetProperty("host", out var hostObj) && hostObj.TryGetProperty("name", out var nameProp))
//                            host = nameProp.GetString() ?? "";
//                        if (string.IsNullOrEmpty(host)) continue;

//                        var minuteKey = ts.ToString("yyyyMMddHHmm");
//                        var hm = perHostMinute.TryGetValue(host, out var m) ? m : (perHostMinute[host] = new Dictionary<string, int>());
//                        hm[minuteKey] = hm.TryGetValue(minuteKey, out var c) ? c + 1 : 1;
//                    }
//                    logger.LogInformation("Features: Parsed {Lines} lines from {File}", fileLines, file);
//                }
//                catch (Exception exFile)
//                {
//                    LogException(logger, $"Parsing apm file '{file}' failed", exFile);
//                }
//            }

//            foreach (var (host, minutes) in perHostMinute)
//            {
//                var q = new P2Quantile(0.95);
//                foreach (var c in minutes.Values) q.Add(c);
//                rpmP95[host] = (float)(double.IsNaN(q.Value) ? 0 : q.Value);
//            }

//            logger.LogInformation("Features: APM hosts with RPM: {Hosts}", rpmP95.Count);
//        }
//        else
//        {
//            logger.LogWarning("Features: APM directory not found or not provided: {Dir}", apmDir ?? "(null)");
//        }

//        // Build HostFeatures
//        const int expectedHours = 24 * 30; // still used for completeness metric
//        var outList = new List<HostFeatures>(acc.Count);

//        foreach (var (host, a) in acc)
//        {
//            var memP95GiB = (float)((double.IsNaN(a.MemP95.Value) ? 0 : a.MemP95.Value) / Math.Pow(1024, 3));
//            var allocGiB = (float)(a.MemTotalMaxBytes / Math.Pow(1024, 3));
//            var cpuP95F = (float)(double.IsNaN(a.CpuP95.Value) ? 0 : a.CpuP95.Value);
//            var netInKbps = (float)((double.IsNaN(a.NetInP95.Value) ? 0 : a.NetInP95.Value) * 8 / 1024.0);
//            var netOutKbps = (float)((double.IsNaN(a.NetOutP95.Value) ? 0 : a.NetOutP95.Value) * 8 / 1024.0);

//            rpmP95.TryGetValue(host, out var rpm);

//            var completeness = expectedHours > 0 ? Math.Clamp(a.Hours.Count / (float)expectedHours, 0f, 1f) : 0f;
//            var idleFrac = a.TotalDocs > 0 ? (float)a.IdleDocs / a.TotalDocs : 0f;
//            var offIdleFr = a.TotalDocs > 0 ? (float)a.OffIdleDocs / a.TotalDocs : 0f;

//            outList.Add(new HostFeatures
//            {
//                Host = host,
//                MemP95GiB = memP95GiB,
//                CpuP95 = cpuP95F,
//                NetInP95Kbps = netInKbps,
//                NetOutP95Kbps = netOutKbps,
//                RpmP95 = rpm,
//                MemHeadroomGiB = Math.Max(0f, allocGiB - memP95GiB),
//                CpuHeadroom = Math.Max(0f, 1f - cpuP95F),
//                IdleFraction = idleFrac,
//                OffhoursIdleFraction = offIdleFr,
//                DataCompleteness = completeness,
//                AllocMemGiB = allocGiB
//            });
//        }

//        logger.LogInformation("Features: Built HostFeatures for {Count} hosts.", outList.Count);
//        return outList;
//    }

//    private static async Task<float[]> GetHourlyCpuSeriesForHostFromFiles(string metricsDir, string host, int days, ILogger logger)
//    {
//        var from = DateTime.UtcNow.AddDays(-days);
//        var perHour = new SortedDictionary<string, (double sum, int n)>();

//        if (!Directory.Exists(metricsDir))
//        {
//            logger.LogWarning("Trend: metricsDir not found: {Dir}", metricsDir);
//            return Array.Empty<float>();
//        }

//        foreach (var file in Directory.EnumerateFiles(metricsDir, "*.ndjson", SearchOption.TopDirectoryOnly))
//        {
//            try
//            {
//                await foreach (var doc in ReadNdjson(file))
//                {
//                    if (!doc.TryGetProperty("@timestamp", out var tsProp)) continue;
//                    var ts = tsProp.GetDateTime().ToUniversalTime();
//                    if (ts < from) continue;

//                    if (!doc.TryGetProperty("host", out var hostObj) || !hostObj.TryGetProperty("name", out var hnameProp)) continue;
//                    var hname = hnameProp.GetString() ?? "";
//                    if (!string.Equals(hname, host, StringComparison.OrdinalIgnoreCase)) continue;

//                    var hourKey = ts.ToString("yyyyMMddHH");

//                    var cpu = TryNum(doc, "system.cpu.total.norm.pct");
//                    if (double.IsNaN(cpu)) cpu = TryNum(doc, "system.process.cpu.total.norm.pct");
//                    if (double.IsNaN(cpu)) continue;

//                    var tup = perHour.TryGetValue(hourKey, out var v) ? v : (0, 0);
//                    tup.sum += cpu; tup.n++;
//                    perHour[hourKey] = tup;
//                }
//            }
//            catch (Exception exFile)
//            {
//                LogException(logger, $"Trend: reading metrics file '{file}' failed", exFile);
//            }
//        }

//        return perHour.Count == 0 ? Array.Empty<float>() : perHour.Values.Select(v => (float)(v.n == 0 ? 0 : v.sum / v.n)).ToArray();
//    }

//    // -------------------- NDJSON helpers --------------------

//    private static async IAsyncEnumerable<JsonElement> ReadNdjson(string path)
//    {
//        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, FileOptions.SequentialScan);
//        using var sr = new StreamReader(fs, Encoding.UTF8);
//        string? line;
//        while ((line = await sr.ReadLineAsync()) != null)
//        {
//            if (string.IsNullOrWhiteSpace(line)) continue;
//            using var doc = JsonDocument.Parse(line);
//            yield return doc.RootElement.Clone();
//        }
//    }

//    private static double TryNum(JsonElement root, string dottedPath)
//    {
//        if (root.TryGetProperty(dottedPath, out var v) && v.ValueKind == JsonValueKind.Number) return v.GetDouble();

//        var parts = dottedPath.Split('.', 2);
//        if (parts.Length == 2 && root.TryGetProperty(parts[0], out var first) && first.ValueKind == JsonValueKind.Object)
//        {
//            if (first.TryGetProperty(parts[1], out var vv) && vv.ValueKind == JsonValueKind.Number) return vv.GetDouble();
//        }
//        return double.NaN;
//    }
//}
