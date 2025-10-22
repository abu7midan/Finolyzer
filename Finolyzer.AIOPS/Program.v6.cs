//// Program.cs
//// .NET 9 Minimal API + ML.NET (cluster-only + runtime anomaly score + trend guard)
//// EXPORT (per-hour, last 30d): fetch TOP N docs **per hour window** for metrics, APM transactions, APM metrics, APM spans, APM errors
//// TRAIN from exported NDJSON files, SUGGEST from files
//// Verbose console logging throughout.
////
//// Endpoints:
////   GET  /health
////   POST /train            -> (optionally) export per-hour (size=N) for last 30d, then train KMeans
////   GET  /suggestions      -> load model, rebuild features+APM signals from NDJSON, compute suggestions
////
//// NuGet: Microsoft.ML (+ Microsoft.ML.Mkl.Components optional)

//using Microsoft.ML;
//using Microsoft.ML.Data;
//using Microsoft.ML.Transforms;
//using System.Text;
//using System.Text.Json;

//using System.Globalization;
//using System.Net.Http.Headers;
//using Microsoft.AspNetCore.Builder;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Hosting;
//using Microsoft.AspNetCore.Http;

//namespace CostAdvisor;

//// -------------------- DTOs --------------------
//public sealed class HostFeatures
//{
//    public string Host { get; set; } = "";

//    // Core features used by ML
//    public float MemP95GiB { get; set; }
//    public float CpuP95 { get; set; }             // 0..1
//    public float NetInP95Kbps { get; set; }
//    public float NetOutP95Kbps { get; set; }
//    public float RpmP95 { get; set; }
//    public float MemHeadroomGiB { get; set; }     // alloc - p95
//    public float CpuHeadroom { get; set; }        // 1 - cpu_p95
//    public float IdleFraction { get; set; }       // docs with cpu<5% / total docs
//    public float OffhoursIdleFraction { get; set; }
//    public float DataCompleteness { get; set; }   // non-empty hours / 720 (approx)
//    public float AllocMemGiB { get; set; }

//    // Extra stats (for messaging)
//    public float CpuMax { get; set; }                 // 0..1
//    public float MemUsedMaxGiB { get; set; }          // max observed (GiB)
//    public float NetInMaxKbps { get; set; }
//    public float NetOutMaxKbps { get; set; }
//    public int HoursObserved { get; set; }
//    public long TotalDocs { get; set; }
//}

//// APM (transactions) traffic & reliability
//public sealed class ApmSignals
//{
//    public string Host { get; set; } = "";
//    public float ErrorRate { get; set; }          // failures / total (from transactions)
//    public float LatencyP95Ms { get; set; }       // ms (from transactions)
//    public float ApproxRpmP95 { get; set; }       // from minute buckets in sample
//    public string? TopService { get; set; }
//}

//// [APM-NEW] GC / CLR metrics snapshot
//public sealed class ApmClrSignals
//{
//    public string Host { get; set; } = "";
//    public float GcCountPerMinP95 { get; set; }
//    public float GcTimePctP95 { get; set; }         // 0..100 (approx)
//    public float Gen2SizeP95GiB { get; set; }
//    public float Gen3SizeP95GiB { get; set; }
//    public float Gen2SizeMaxGiB { get; set; }
//    public float Gen3SizeMaxGiB { get; set; }
//}

//// [APM-NEW] Integrations & exceptions snapshot
//public sealed class ApmIntegrationSignals
//{
//    public string Host { get; set; } = "";
//    public int FailedHttpSpans { get; set; }
//    public int FailedDbSpans { get; set; }
//    public int FailedCacheSpans { get; set; }
//    public string? TopExternalService { get; set; }
//    public int TotalErrorDocs { get; set; }
//    public string? TopExceptionType { get; set; }
//    public string? TopExceptionMessage { get; set; }
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

//// -------------------- ML Trainer (cluster only; anomaly computed at runtime) --------------------
//public static class ModelTrainer
//{
//    private const string ClusterModelFile = "cluster_v2.zip";

//    public static ITransformer TrainClusterOnly(MLContext ml, List<HostFeatures> rows, string modelDir, ILogger logger)
//    {
//        var clean = Sanitize(rows, logger);
//        var uniq = clean.Select(r => $"{r.CpuP95:G9}|{r.MemP95GiB:G9}|{r.RpmP95:G9}|{r.NetInP95Kbps:G9}|{r.NetOutP95Kbps:G9}").Distinct().Count();

//        ITransformer model;
//        if (clean.Count < 3 || uniq < 2)
//        {
//            logger.LogWarning("KMeans: few unique rows ({Unique}), training k=1.", uniq);
//            model = FitKMeans(ml, clean, 1, logger);
//        }
//        else
//        {
//            var k = Math.Min(8, Math.Max(2, Math.Min(uniq, clean.Count - 1)));
//            model = FitKMeans(ml, clean, k, logger);
//        }

//        Directory.CreateDirectory(modelDir);
//        var path = Path.Combine(modelDir, ClusterModelFile);
//        var schema = ml.Data.LoadFromEnumerable(clean).Schema;
//        ml.Model.Save(model, schema, path);
//        logger.LogInformation("Saved cluster model -> {Path}", path);
//        return model;
//    }

//    private static ITransformer FitKMeans(MLContext ml, List<HostFeatures> rows, int k, ILogger logger)
//    {
//        var data = ml.Data.LoadFromEnumerable(rows);
//        var pairs = Columns.ClusterCols.Select(c => new InputOutputColumnPair(c, c)).ToArray();

//        var pipeline =
//            ml.Transforms.ReplaceMissingValues(pairs, MissingValueReplacingEstimator.ReplacementMode.Mean)
//              .Append(ml.Transforms.NormalizeMinMax(pairs))
//              .Append(ml.Transforms.Concatenate("Features", Columns.ClusterCols))
//              .Append(ml.Clustering.Trainers.KMeans("Features", numberOfClusters: k));

//        try
//        {
//            logger.LogInformation("Fitting KMeans (k={K})...", k);
//            return pipeline.Fit(data);
//        }
//        catch (Exception ex)
//        {
//            logger.LogWarning(ex, "KMeans failed, retry with k=1");
//            var pipe2 =
//                ml.Transforms.ReplaceMissingValues(pairs, MissingValueReplacingEstimator.ReplacementMode.Mean)
//                .Append(ml.Transforms.NormalizeMinMax(pairs))
//                .Append(ml.Transforms.Concatenate("Features", Columns.ClusterCols))
//                .Append(ml.Clustering.Trainers.KMeans("Features", numberOfClusters: 1));
//            return pipe2.Fit(data);
//        }
//    }

//    private static List<HostFeatures> Sanitize(List<HostFeatures> rows, ILogger logger)
//    {
//        static float Clamp(double v, double min, double max)
//        {
//            if (double.IsNaN(v) || double.IsInfinity(v)) v = 0d;
//            if (v < min) v = min; else if (v > max) v = max;
//            return (float)v;
//        }
//        int before = rows.Count;
//        foreach (var r in rows)
//        {
//            r.MemP95GiB = Clamp(r.MemP95GiB, 0, 1e6);
//            r.CpuP95 = Clamp(r.CpuP95, 0, 1);
//            r.NetInP95Kbps = Clamp(r.NetInP95Kbps, 0, 1e9);
//            r.NetOutP95Kbps = Clamp(r.NetOutP95Kbps, 0, 1e9);
//            r.RpmP95 = Clamp(r.RpmP95, 0, 1e9);
//            r.MemHeadroomGiB = Clamp(r.MemHeadroomGiB, 0, 1e6);
//            r.CpuHeadroom = Clamp(r.CpuHeadroom, 0, 1);
//            r.IdleFraction = Clamp(r.IdleFraction, 0, 1);
//            r.OffhoursIdleFraction = Clamp(r.OffhoursIdleFraction, 0, 1);
//            r.DataCompleteness = Clamp(r.DataCompleteness, 0, 1);
//            r.AllocMemGiB = Clamp(r.AllocMemGiB, 0, 1e6);
//            r.CpuMax = Clamp(r.CpuMax, 0, 1);
//            r.MemUsedMaxGiB = Clamp(r.MemUsedMaxGiB, 0, 1e6);
//            r.NetInMaxKbps = Clamp(r.NetInMaxKbps, 0, 1e9);
//            r.NetOutMaxKbps = Clamp(r.NetOutMaxKbps, 0, 1e9);
//        }
//        rows.RemoveAll(r => string.IsNullOrWhiteSpace(r.Host));
//        int after = rows.Count;
//        if (after < before)
//            logger.LogWarning("Sanitize: dropped {N} rows", before - after);
//        return rows;
//    }

//    public static string ClusterModelPath(string modelDir) => Path.Combine(modelDir, ClusterModelFile);
//}

//// -------------------- Runtime anomaly scoring --------------------
//static class AnomalyScoring
//{
//    public static Dictionary<string, (double mean, double std)> ComputeStats(List<HostFeatures> rows, string[] cols)
//    {
//        var stats = new Dictionary<string, (double mean, double std)>(StringComparer.Ordinal);
//        foreach (var c in cols)
//        {
//            double sum = 0, sum2 = 0; int n = 0;
//            foreach (var r in rows)
//            {
//                var v = c switch
//                {
//                    nameof(HostFeatures.MemHeadroomGiB) => r.MemHeadroomGiB,
//                    nameof(HostFeatures.CpuHeadroom) => r.CpuHeadroom,
//                    nameof(HostFeatures.IdleFraction) => r.IdleFraction,
//                    nameof(HostFeatures.OffhoursIdleFraction) => r.OffhoursIdleFraction,
//                    nameof(HostFeatures.RpmP95) => r.RpmP95,
//                    nameof(HostFeatures.NetInP95Kbps) => r.NetInP95Kbps,
//                    nameof(HostFeatures.NetOutP95Kbps) => r.NetOutP95Kbps,
//                    _ => 0
//                };
//                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
//                sum += v; sum2 += v * v; n++;
//            }
//            if (n == 0) stats[c] = (0, 1);
//            else
//            {
//                var mean = sum / n;
//                var var_ = Math.Max(1e-12, sum2 / n - mean * mean);
//                stats[c] = (mean, Math.Sqrt(var_));
//            }
//        }
//        return stats;
//    }

//    public static AnomalyOutput Score(HostFeatures r, Dictionary<string, (double mean, double std)> stats, string[] cols)
//    {
//        double sumSq = 0;
//        foreach (var c in cols)
//        {
//            var (mean, std) = stats.TryGetValue(c, out var s) ? s : (0d, 1d);
//            double v = c switch
//            {
//                nameof(HostFeatures.MemHeadroomGiB) => r.MemHeadroomGiB,
//                nameof(HostFeatures.CpuHeadroom) => r.CpuHeadroom,
//                nameof(HostFeatures.IdleFraction) => r.IdleFraction,
//                nameof(HostFeatures.OffhoursIdleFraction) => r.OffhoursIdleFraction,
//                nameof(HostFeatures.RpmP95) => r.RpmP95,
//                nameof(HostFeatures.NetInP95Kbps) => r.NetInP95Kbps,
//                nameof(HostFeatures.NetOutP95Kbps) => r.NetOutP95Kbps,
//                _ => 0
//            };
//            var z = (v - mean) / (std <= 0 ? 1 : std);
//            sumSq += z * z;
//        }
//        var dim = Math.Max(1, cols.Length);
//        var l2 = Math.Sqrt(sumSq) / Math.Sqrt(dim);
//        var score = (float)(l2 / (1.0 + l2));
//        return new AnomalyOutput { PredictedLabel = false, Score = score, PValue = 1f - score };
//    }
//}

//// -------------------- Trend guard --------------------
//public static class TrendGuard
//{
//    public static bool IsTrendingUp(float[] cpuSeries, int minPoints = 24 * 7, double slopeThreshold = 0.0007)
//    {
//        if (cpuSeries == null || cpuSeries.Length < minPoints) return false;
//        int n = cpuSeries.Length;
//        double meanX = (n - 1) / 2.0;
//        double meanY = cpuSeries.Average();
//        double num = 0, den = 0;
//        for (int i = 0; i < n; i++) { double dx = i - meanX; double dy = cpuSeries[i] - meanY; num += dx * dy; den += dx * dx; }
//        double slope = den == 0 ? 0 : num / den;
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
//        if (count < 5) { x[count++] = v; if (count == 5) { Array.Sort(x); n[0] = 1; n[1] = 2; n[2] = 3; n[3] = 4; n[4] = 5; np[0] = 1; np[1] = 1 + 2 * q; np[2] = 1 + 4 * q; np[3] = 3 + 2 * q; np[4] = 5; } return; }
//        int k = v < x[0] ? 0 : v >= x[4] ? 3 : Array.FindLastIndex(x, xi => v >= xi);
//        if (v < x[0]) x[0] = v; else if (v >= x[4]) x[4] = v;
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
//            if (count < 5) { var sorted = x.Take(count).OrderBy(v => v).ToArray(); var idx = (int)Math.Floor((count - 1) * q); return sorted[idx]; }
//            return x[2];
//        }
//    }
//}

//// -------------------- API --------------------
//public class Program
//{
//    // ======= Config =======
//    internal static string EsUrl => Environment.GetEnvironmentVariable("ES_URL") ?? "https://ELK.THIQAH.SA";
//    internal static string EsApiKeyBase64 => Environment.GetEnvironmentVariable("ES_API_KEY_BASE64") ?? "enhTTEJwb0JTRmJRbGFOT0RVdmY6YlZmd1BwamZTQWVVTTQ2T2Nlek94Zw==";
//    internal static string MetricsIndex => Environment.GetEnvironmentVariable("MB_INDEX") ?? ".ds-metricbeat-*,metricbeat-*,metricbeat-psintg-*,metricbeat-rabbitmq-*,metrics-*-elk,*metricbeat*,*metric*";
//    internal static string ApmTxIndex => Environment.GetEnvironmentVariable("APM_TX_INDEX") ?? "traces-apm*,apm-*";
//    internal static string ApmMetricsIndex => Environment.GetEnvironmentVariable("APM_METRIC_INDEX") ?? "apm-*-metric-*"; // [APM-NEW]
//    internal static string ApmSpanIndex => Environment.GetEnvironmentVariable("APM_SPAN_INDEX") ?? "apm-*-span-*";       // [APM-NEW]
//    internal static string ApmErrorIndex => Environment.GetEnvironmentVariable("APM_ERROR_INDEX") ?? "apm-*-error-*";    // [APM-NEW]

//    // Export destinations
//    internal static string MetricsOutDir => "./data/metrics";
//    internal static string ApmTxOutDir => "./data/apm_tx";
//    internal static string ApmMetricsOutDir => "./data/apm_metrics";   // [APM-NEW]
//    internal static string ApmSpanOutDir => "./data/apm_spans";        // [APM-NEW]
//    internal static string ApmErrorOutDir => "./data/apm_errors";      // [APM-NEW]

//    // Thresholds
//    internal const float ErrorRateWarn = 0.015f;     // 1.5%
//    internal const float LatencyP95WarnMs = 1000f;   // 1.0s
//    internal const float RpmPresentThreshold = 0.5f;

//    internal const float CpuHighThreshold = 0.75f;   // 75% p95
//    internal const float CpuVeryHighMax = 0.90f;     // 90% max
//    internal const float MemHeadroomLowGiB = 0.5f;   // < 0.5 GiB headroom
//    internal const float MemTightRatio = 0.85f;      // p95/alloc >= 85%

//    // GC heuristics
//    internal const float GcTimePctWarn = 10f;        // p95 GC% over sampled mins
//    internal const float Gen23ShareWarn = 0.60f;     // (gen2+gen3) / alloc > 60% → LOH/fragmentation suspicion
//    internal const int FailedSpanWarn = 5;         // per 30d sample

//    // Per-hour export cap
//    internal const int PerHourSize = 100;

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

//        // Export + Train
//        app.MapPost("/train", async () =>
//        {
//            try
//            {
//                var toUtc = DateTime.UtcNow;
//                var fromUtc = toUtc.AddDays(-30);
//                logger.LogInformation("POST /train export size={Size} {From} -> {To}", PerHourSize, fromUtc, toUtc);

//                Directory.CreateDirectory(MetricsOutDir);
//                Directory.CreateDirectory(ApmTxOutDir);
//                Directory.CreateDirectory(ApmMetricsOutDir);
//                Directory.CreateDirectory(ApmSpanOutDir);
//                Directory.CreateDirectory(ApmErrorOutDir);

//                // Set to true to refresh export (kept false to avoid long runs)
//                //if (true)
//                //{
//                //    await ExportPerHourLimitedNdjson(MetricsIndex, fromUtc, toUtc, MetricsOutDir, BuildMetricsQueryBodyLimited, PerHourSize, logger);
//                //    await ExportPerHourLimitedNdjson(ApmTxIndex, fromUtc, toUtc, ApmTxOutDir, BuildApmTxQueryBodyLimited, PerHourSize, logger);
//                //    await ExportPerHourLimitedNdjson(ApmMetricsIndex, fromUtc, toUtc, ApmMetricsOutDir, BuildApmClrMetricQueryBodyLimited, PerHourSize, logger); // [APM-NEW]
//                //    await ExportPerHourLimitedNdjson(ApmSpanIndex, fromUtc, toUtc, ApmSpanOutDir, BuildApmSpanQueryBodyLimited, PerHourSize, logger);           // [APM-NEW]
//                //    await ExportPerHourLimitedNdjson(ApmErrorIndex, fromUtc, toUtc, ApmErrorOutDir, BuildApmErrorQueryBodyLimited, PerHourSize, logger);        // [APM-NEW]
//                //}
//                // Set to true to refresh export
//                if (true)
//                {
//                    // Each call handles: build per-day hour tasks -> Task.WaitAll(...) per day -> return after all days done
//                    ExportPerHourLimitedNdjsonParallel(MetricsIndex, fromUtc, toUtc, MetricsOutDir, BuildMetricsQueryBodyLimited, PerHourSize, logger, maxDegreeOfParallelism: 6);
//                    ExportPerHourLimitedNdjsonParallel(ApmTxIndex, fromUtc, toUtc, ApmTxOutDir, BuildApmTxQueryBodyLimited, PerHourSize, logger, maxDegreeOfParallelism: 6);
//                    ExportPerHourLimitedNdjsonParallel(ApmMetricsIndex, fromUtc, toUtc, ApmMetricsOutDir, BuildApmClrMetricQueryBodyLimited, PerHourSize, logger, maxDegreeOfParallelism: 6); // [APM-NEW]
//                    ExportPerHourLimitedNdjsonParallel(ApmSpanIndex, fromUtc, toUtc, ApmSpanOutDir, BuildApmSpanQueryBodyLimited, PerHourSize, logger, maxDegreeOfParallelism: 6); // [APM-NEW]
//                    ExportPerHourLimitedNdjsonParallel(ApmErrorIndex, fromUtc, toUtc, ApmErrorOutDir, BuildApmErrorQueryBodyLimited, PerHourSize, logger, maxDegreeOfParallelism: 6); // [APM-NEW]
//                }

//                logger.LogInformation("POST /train: Building features...");
//                var ml = new MLContext(seed: 42);
//                var (features, apm, apmClr, apmInt) = await BuildFeaturesAndApmFromFiles(
//                    MetricsOutDir, ApmTxOutDir, ApmMetricsOutDir, ApmSpanOutDir, ApmErrorOutDir, logger);

//                if (features.Count == 0)
//                    return Results.Ok(new { trained = 0, note = "No features from files." });

//                ModelTrainer.TrainClusterOnly(ml, features, "models", logger);
//                return Results.Ok(new { trained = features.Count, when = DateTime.UtcNow });
//            }
//            catch (Exception ex)
//            {
//                LogException(logger, "POST /train failed", ex);
//                return Results.Problem(ex.Message);
//            }
//        });

//        // Suggestions
//        app.MapGet("/suggestions", async () =>
//        {
//            try
//            {
//                logger.LogInformation("GET /suggestions: load features+APM...");
//                var ml = new MLContext();
//                var (features, apm, apmClr, apmInt) = await BuildFeaturesAndApmFromFiles(
//                    MetricsOutDir, ApmTxOutDir, ApmMetricsOutDir, ApmSpanOutDir, ApmErrorOutDir, logger);

//                if (features.Count == 0)
//                    return Results.Ok(new { generated_at = DateTime.UtcNow, results = Array.Empty<object>(), note = "No features. Train first." });

//                var clusterPath = ModelTrainer.ClusterModelPath("models");
//                if (!File.Exists(clusterPath))
//                    return Results.Problem("Model not found. Call /train first.");

//                using var fsC = File.OpenRead(clusterPath);
//                var clusterModel = ml.Model.Load(fsC, out _);
//                var clusterEngine = ml.Model.CreatePredictionEngine<HostFeatures, ClusterOutput>(clusterModel);

//                var anomalyStats = AnomalyScoring.ComputeStats(features, Columns.AnomalyCols);

//                var results = new List<object>();
//                foreach (var h in features)
//                {
//                    try
//                    {
//                        apm.TryGetValue(h.Host, out var apmSig);
//                        apmClr.TryGetValue(h.Host, out var clrSig);
//                        apmInt.TryGetValue(h.Host, out var intSig);

//                        var ao = AnomalyScoring.Score(h, anomalyStats, Columns.AnomalyCols);
//                        var co = clusterEngine.Predict(h);
//                        var cpuSeries = await GetHourlyCpuSeriesForHostFromFiles(MetricsOutDir, h.Host, 7, logger);
//                        var trendingUp = TrendGuard.IsTrendingUp(cpuSeries);

//                        var suggs = new List<object>();

//                        // === Scale-out ===
//                        if ((h.CpuP95 >= CpuHighThreshold && (apmSig?.ApproxRpmP95 ?? h.RpmP95) >= RpmPresentThreshold) ||
//                            ((apmSig?.LatencyP95Ms ?? 0) >= LatencyP95WarnMs && (apmSig?.ApproxRpmP95 ?? h.RpmP95) >= RpmPresentThreshold && (apmSig?.ErrorRate ?? 0) < 0.02))
//                        {
//                            var reason = h.CpuP95 >= CpuHighThreshold ? $"CPU p95 {h.CpuP95:P0}" : $"Latency p95 {(apmSig?.LatencyP95Ms ?? 0):0} ms";
//                            suggs.Add(new { type = "scale_out", message = $"{h.Host}: consider scaling out. Reason: {reason}, ~{(apmSig?.ApproxRpmP95 ?? h.RpmP95):0} rpm.", confidence = 0.72 });
//                        }

//                        // === Increase CPU ===
//                        if (h.CpuP95 >= CpuHighThreshold || h.CpuMax >= CpuVeryHighMax)
//                        {
//                            var targetCpuTier = MapCpuToTierUp(h.CpuP95, h.CpuMax);
//                            suggs.Add(new { type = "increase_cpu", message = $"{h.Host}: CPU high (p95 {h.CpuP95 * 100:0.#}% , max {h.CpuMax * 100:0.#}%) → bump to {targetCpuTier}.", confidence = 0.7 });
//                        }

//                        // === Increase Memory ===
//                        if (h.MemHeadroomGiB <= MemHeadroomLowGiB && h.AllocMemGiB > 0 && (h.MemP95GiB / Math.Max(h.AllocMemGiB, 0.01f)) >= MemTightRatio)
//                        {
//                            var target = Math.Max((int)Math.Ceiling(h.MemP95GiB * 1.25), (int)Math.Ceiling(h.AllocMemGiB));
//                            suggs.Add(new { type = "increase_memory", message = $"{h.Host}: memory near saturation p95 {h.MemP95GiB:0.0} GiB, max {h.MemUsedMaxGiB:0.0} GiB / alloc {h.AllocMemGiB:0.0} GiB → consider ≈ {target} GiB.", confidence = 0.7 });
//                        }

//                        // === Rightsize down ===
//                        if ((IsIdleCluster(co.ClusterId) || ao.Score >= 0.7f) && !trendingUp && h.DataCompleteness >= 0.9f)
//                        {
//                            var targetGiB = Math.Max(2, (int)Math.Ceiling(h.MemP95GiB * 1.25));
//                            suggs.Add(new { type = "rightsize_memory_down", message = $"{h.Host}: under-utilized memory; p95 {h.MemP95GiB:0.0} GiB vs alloc {h.AllocMemGiB:0.0} GiB → set ≈ {targetGiB} GiB.", confidence = Confidence(h, ao, trendingUp) });
//                            var downTier = MapCpuToTierDown(h.CpuP95);
//                            suggs.Add(new { type = "rightsize_cpu_down", message = $"{h.Host}: CPU p95 {h.CpuP95 * 100:0.#}% → smaller tier {downTier}.", confidence = Confidence(h, ao, trendingUp) });
//                        }

//                        // === Off-hours stop ===
//                        if (h.OffhoursIdleFraction >= 0.8f && !trendingUp)
//                            suggs.Add(new { type = "offhours_shutdown", message = $"{h.Host}: off-hours idle {h.OffhoursIdleFraction:P0} → schedule stop 00:00–07:00.", confidence = 0.65 });

//                        // === Consolidate / shutdown ===
//                        if (h.RpmP95 < 0.5 && h.CpuP95 < 0.04f && (h.NetInP95Kbps + h.NetOutP95Kbps) < 8)
//                            suggs.Add(new { type = "consolidate_or_shutdown", message = $"{h.Host}: near-zero traffic (rpm {h.RpmP95:0}), CPU p95 {h.CpuP95 * 100:0.#}%, net p95 {(h.NetInP95Kbps + h.NetOutP95Kbps):0.#} kbps.", confidence = 0.65 });

//                        // === APM error rate / latency ===
//                        if (apmSig is not null && apmSig.ErrorRate >= ErrorRateWarn && apmSig.ApproxRpmP95 >= RpmPresentThreshold)
//                            suggs.Add(new { type = "apm_error_rate", message = $"{h.Host}: high txn error rate {apmSig.ErrorRate:P1} (svc: {apmSig.TopService ?? "unknown"}). Investigate exceptions.", confidence = 0.8 });
//                        if (apmSig is not null && apmSig.LatencyP95Ms >= LatencyP95WarnMs && apmSig.ApproxRpmP95 >= RpmPresentThreshold)
//                            suggs.Add(new { type = "apm_high_latency", message = $"{h.Host}: txn p95 latency {apmSig.LatencyP95Ms:0} ms under ~{apmSig.ApproxRpmP95:0} rpm. Check DB/IO, N+1, caching, or scale-out.", confidence = 0.68 });

//                        // === APM missing ===
//                        if (apmSig is null && (h.RpmP95 >= 0.5 || h.CpuP95 >= 0.10f))
//                            suggs.Add(new { type = "apm_instrumentation", message = $"{h.Host}: activity present but no APM transactions found → enable APM agent/auto-instrumentation.", confidence = 0.6 });

//                        // [APM-NEW] === CLR / GC pressure ===
//                        if (clrSig is not null)
//                        {
//                            if (clrSig.GcTimePctP95 >= GcTimePctWarn)
//                                suggs.Add(new { type = "gc_pressure", message = $"{h.Host}: high GC time (p95 ~{clrSig.GcTimePctP95:0.#}% of CPU). Reduce allocations, pool objects, review LOH usage.", confidence = 0.7 });

//                            // Gen2+Gen3 as share of alloc mem
//                            var allocBytes = h.AllocMemGiB * 1024f * 1024f * 1024f;
//                            if (allocBytes > 0)
//                            {
//                                var gen23P95GiB = clrSig.Gen2SizeP95GiB + clrSig.Gen3SizeP95GiB;
//                                var share = gen23P95GiB / Math.Max(1e-6f, h.AllocMemGiB);
//                                if (share >= Gen23ShareWarn)
//                                    suggs.Add(new { type = "memory_fragmentation_or_leak", message = $"{h.Host}: Gen2+Gen3 ≈ {gen23P95GiB:0.00} GiB (~{share * 100:0.#}% of alloc). Possible LOH/fragmentation → review large arrays/strings, pinning, caching.", confidence = 0.68 });
//                            }

//                            if (clrSig.GcCountPerMinP95 >= 30) // aggressive churn
//                                suggs.Add(new { type = "excessive_gc_frequency", message = $"{h.Host}: GC count p95 ~{clrSig.GcCountPerMinP95:0} / min. Consider reducing per-request allocations, reuse buffers.", confidence = 0.65 });
//                        }

//                        // [APM-NEW] === Integrations & exceptions ===
//                        if (intSig is not null)
//                        {
//                            if (intSig.FailedHttpSpans >= FailedSpanWarn)
//                                suggs.Add(new { type = "integration_http_failures", message = $"{h.Host}: frequent HTTP span failures (~{intSig.FailedHttpSpans}). Check timeouts/DNS/SSL. Top ext: {intSig.TopExternalService ?? "n/a"}.", confidence = 0.72 });
//                            if (intSig.FailedDbSpans >= FailedSpanWarn)
//                                suggs.Add(new { type = "integration_db_failures", message = $"{h.Host}: frequent DB span failures (~{intSig.FailedDbSpans}). Verify connection pools, queries, locks.", confidence = 0.72 });
//                            if (intSig.FailedCacheSpans >= FailedSpanWarn)
//                                suggs.Add(new { type = "integration_cache_failures", message = $"{h.Host}: frequent cache span failures (~{intSig.FailedCacheSpans}). Check Redis/memcached endpoints and network.", confidence = 0.7 });

//                            if (intSig.TotalErrorDocs >= FailedSpanWarn && (!string.IsNullOrEmpty(intSig.TopExceptionType) || !string.IsNullOrEmpty(intSig.TopExceptionMessage)))
//                                suggs.Add(new { type = "apm_exceptions", message = $"{h.Host}: exceptions observed ({intSig.TotalErrorDocs}). Top: {intSig.TopExceptionType ?? "unknown"} {(intSig.TopExceptionMessage ?? "").Trim()}", confidence = 0.75 });
//                        }

//                        // Build metrics block
//                        var metrics = new
//                        {
//                            cpu_p95 = Math.Round(h.CpuP95 * 100, 2),
//                            cpu_max = Math.Round(h.CpuMax * 100, 2),
//                            mem_p95_gib = Math.Round(h.MemP95GiB, 2),
//                            mem_used_max_gib = Math.Round(h.MemUsedMaxGiB, 2),
//                            mem_alloc_gib = Math.Round(h.AllocMemGiB, 2),
//                            net_in_p95_kbps = Math.Round(h.NetInP95Kbps, 2),
//                            net_out_p95_kbps = Math.Round(h.NetOutP95Kbps, 2),
//                            net_in_max_kbps = Math.Round(h.NetInMaxKbps, 2),
//                            net_out_max_kbps = Math.Round(h.NetOutMaxKbps, 2),
//                            rpm_p95 = Math.Round(h.RpmP95, 2),
//                            apm_error_rate = apmSig?.ErrorRate,
//                            apm_latency_p95_ms = apmSig?.LatencyP95Ms,
//                            hours_observed = h.HoursObserved,
//                            total_docs = h.TotalDocs,
//                            // [APM-NEW] CLR & Integrations
//                            clr_gc_time_p95_pct = clrSig?.GcTimePctP95,
//                            clr_gen2_p95_gib = clrSig?.Gen2SizeP95GiB,
//                            clr_gen3_p95_gib = clrSig?.Gen3SizeP95GiB,
//                            failed_http_spans = intSig?.FailedHttpSpans,
//                            failed_db_spans = intSig?.FailedDbSpans,
//                            failed_cache_spans = intSig?.FailedCacheSpans,
//                            top_exception = intSig?.TopExceptionType
//                        };

//                        results.Add(new { host = h.Host, metrics, suggestions = suggs });
//                    }
//                    catch (Exception exHost)
//                    {
//                        LogException(logger, $"Suggest host '{h.Host}' failed", exHost);
//                    }
//                }

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

//    // -------------------- Helpers --------------------
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

//    private static string MapCpuToTierUp(float cpuP95, float cpuMax) =>
//        (cpuP95, cpuMax) switch
//        {
//            ( >= 0.85f, _) or (_, >= 0.95f) => "XL (16 vCPU)",
//            ( >= 0.75f, _) or (_, >= 0.90f) => "L (8 vCPU)",
//            _ => "M (4 vCPU)"
//        };
//    private static void LogException(ILogger logger, string prefix, Exception ex)
//        => logger.LogError(ex, "{Prefix}: {Message}", prefix, ex.Message);

//    // -------------------- ES per-hour limited fetch --------------------
//    private static HttpClient CreateEsHttp(ILogger logger)
//    {
//        logger.LogInformation("ES HttpClient -> {Url}", EsUrl);
//        var http = new HttpClient { BaseAddress = new Uri(EsUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(3) };
//        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", EsApiKeyBase64);
//        return http;
//    }
//    // ==================== PARALLEL PER-DAY / PER-HOUR EXPORT ====================
//    // ==================== PARALLEL PER-DAY / PER-HOUR EXPORT ====================
//    private static void ExportPerHourLimitedNdjsonParallel(
//        string indexPattern,
//        DateTime fromUtc,
//        DateTime toUtc,
//        string outDir,
//        Func<DateTime, DateTime, int, string> buildLimitedBody,
//        int size,
//        ILogger logger,
//        int maxDegreeOfParallelism = 6) // throttle to protect ES
//    {
//        Directory.CreateDirectory(outDir);
//        using var http = CreateEsHttp(logger);

//        // Align to the hour
//        fromUtc = new DateTime(fromUtc.Year, fromUtc.Month, fromUtc.Day, fromUtc.Hour, 0, 0, DateTimeKind.Utc);
//        toUtc = new DateTime(toUtc.Year, toUtc.Month, toUtc.Day, toUtc.Hour, 0, 0, DateTimeKind.Utc);

//        var esIndexSafe = string.Concat(indexPattern.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')).Trim('_');
//        var throttle = new SemaphoreSlim(maxDegreeOfParallelism);

//        for (var day = fromUtc.Date; day <= toUtc.Date; day = day.AddDays(1))
//        {
//            var dayStart = day;
//            var dayEnd = day.AddDays(1);

//            // Hours within the requested window
//            var hours = new List<DateTime>();
//            for (var h = dayStart; h < dayEnd && h < toUtc; h = h.AddHours(1))
//                if (h >= fromUtc) hours.Add(h);

//            if (hours.Count == 0) continue;

//            logger.LogInformation("────────────────────────────────────────────────────────");
//            logger.LogInformation("DAY {DayStamp} | Building tasks for {Count} hour windows | Index={Index} | Size={Size}",
//                dayStart.ToString("yyyy-MM-dd"), hours.Count, indexPattern, size);
//            logger.LogInformation("────────────────────────────────────────────────────────");

//            var taskList = new List<Task>(hours.Count);
//            var dailyStopwatch = System.Diagnostics.Stopwatch.StartNew();
//            var dailySuccess = 0;
//            var dailySkipped = 0;
//            var dailyFailed = 0;
//            long dailyBytes = 0;

//            foreach (var windowStart in hours)
//            {
//                var ws = windowStart; // capture
//                var we = ws.AddHours(1);
//                var hourLabel = ws.ToString("dd-MM HH:mm");      // e.g., "21-09 00:00"
//                var fileName = Path.Combine(outDir, $"{esIndexSafe}_{ws:yyyyMMdd_HH}.ndjson");

//                logger.LogInformation("[{Hour}] Task CREATED -> {File}", hourLabel, fileName);

//                if (File.Exists(fileName))
//                {
//                    logger.LogInformation("[{Hour}] Task SKIPPED (file exists) -> {File}", hourLabel, fileName);
//                    dailySkipped++;
//                    continue;
//                }

//                var t = Task.Run(async () =>
//                {
//                    await throttle.WaitAsync();
//                    var sw = System.Diagnostics.Stopwatch.StartNew();
//                    try
//                    {
//                        logger.LogInformation("[{Hour}] Task START   | Window {From} → {To} | Size={Size}",
//                            hourLabel, ws.ToString("o"), we.ToString("o"), size);

//                        var body = buildLimitedBody(ws, we, size);
//                        using var req = new HttpRequestMessage(HttpMethod.Post, $"{indexPattern}/_search")
//                        { Content = new StringContent(body, Encoding.UTF8, "application/json") };

//                        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
//                        res.EnsureSuccessStatusCode();

//                        await using var resStream = await res.Content.ReadAsStreamAsync();
//                        using var doc = await JsonDocument.ParseAsync(resStream);

//                        await using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
//                        using var writer = new StreamWriter(fs, new UTF8Encoding(false));

//                        int docs = 0;
//                        if (doc.RootElement.TryGetProperty("hits", out var hits) &&
//                            hits.TryGetProperty("hits", out var arr) && arr.ValueKind == JsonValueKind.Array)
//                        {
//                            foreach (var h in arr.EnumerateArray())
//                            {
//                                if (h.TryGetProperty("_source", out var src))
//                                {
//                                    await writer.WriteLineAsync(src.GetRawText());
//                                    docs++;
//                                }
//                            }
//                        }

//                        await writer.FlushAsync();
//                        sw.Stop();

//                        long written = 0;
//                        try { written = new FileInfo(fileName).Length; } catch { /* ignore */ }

//                        Interlocked.Add(ref dailyBytes, written);
//                        Interlocked.Increment(ref dailySuccess);

//                        logger.LogInformation("[{Hour}] Task DONE    | docs={Docs} bytes={Bytes} in {Ms} ms -> {File}",
//                            hourLabel, docs, written, sw.ElapsedMilliseconds, fileName);
//                    }
//                    catch (Exception ex)
//                    {
//                        sw.Stop();
//                        Interlocked.Increment(ref dailyFailed);
//                        LogException(logger, $"[{hourLabel}] Task FAILED after {sw.ElapsedMilliseconds} ms", ex);

//                        try { if (File.Exists(fileName)) File.Delete(fileName); } catch { /* ignore */ }
//                    }
//                    finally
//                    {
//                        throttle.Release();
//                    }
//                });

//                taskList.Add(t);
//            }

//            if (taskList.Count == 0)
//            {
//                logger.LogInformation("DAY {DayStamp} | No tasks to execute (all skipped/existing).", dayStart.ToString("yyyy-MM-dd"));
//                continue;
//            }

//            logger.LogInformation("DAY {DayStamp} | EXECUTING {Count} hour tasks in parallel (maxDOP={DOP})...",
//                dayStart.ToString("yyyy-MM-dd"), taskList.Count, maxDegreeOfParallelism);

//            // *** EXACTLY as requested: wait for the per-day list of tasks ***
//            Task.WaitAll(taskList.ToArray());

//            dailyStopwatch.Stop();
//            logger.LogInformation("DAY {DayStamp} | SUMMARY: ok={Ok} skipped={Skipped} failed={Failed} bytes={Bytes} in {Secs:0.00}s",
//                dayStart.ToString("yyyy-MM-dd"), dailySuccess, dailySkipped, dailyFailed, dailyBytes, dailyStopwatch.Elapsed.TotalSeconds);
//            logger.LogInformation("────────────────────────────────────────────────────────");
//        }
//    }

//    private static async Task ExportPerHourLimitedNdjson(
//        string indexPattern,
//        DateTime fromUtc,
//        DateTime toUtc,
//        string outDir,
//        Func<DateTime, DateTime, int, string> buildLimitedBody,
//        int size,
//        ILogger logger)
//    {
//        Directory.CreateDirectory(outDir);
//        using var http = CreateEsHttp(logger);
//        for (var windowStart = fromUtc; windowStart < toUtc; windowStart = windowStart.AddHours(1))
//        {
//            var windowEnd = windowStart.AddHours(1);
//            var fileName = Path.Combine(outDir, $"{SanitizeIndex(indexPattern)}_{windowStart:yyyyMMdd_HH}.ndjson");
//            if (File.Exists(fileName)) { logger.LogInformation("Skip exists {File}", fileName); continue; }

//            try
//            {
//                var body = buildLimitedBody(windowStart, windowEnd, size);
//                using var req = new HttpRequestMessage(HttpMethod.Post, $"{indexPattern}/_search")
//                { Content = new StringContent(body, Encoding.UTF8, "application/json") };
//                using var res = await http.SendAsync(req);
//                res.EnsureSuccessStatusCode();

//                using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
//                await using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
//                using var writer = new StreamWriter(fs, new UTF8Encoding(false));

//                if (doc.RootElement.TryGetProperty("hits", out var hits) &&
//                    hits.TryGetProperty("hits", out var arr) && arr.ValueKind == JsonValueKind.Array)
//                {
//                    foreach (var h in arr.EnumerateArray())
//                        if (h.TryGetProperty("_source", out var src))
//                            await writer.WriteLineAsync(src.GetRawText());
//                }
//                logger.LogInformation("Wrote {File}", fileName);
//            }
//            catch (Exception ex) { LogException(logger, $"Export hour {windowStart:yyyy-MM-dd HH}", ex); }
//        }
//        static string SanitizeIndex(string idx) => string.Concat(idx.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')).Trim('_');
//    }

//    private static string BuildMetricsQueryBodyLimited(DateTime gte, DateTime lt, int size)
//    {
//        var gteS = gte.ToString("o", CultureInfo.InvariantCulture);
//        var ltS = lt.ToString("o", CultureInfo.InvariantCulture);
//        return $@"{{
//  ""size"": {size},
//  ""_source"": [
//    ""@timestamp"", ""host.name"",
//    ""system.memory.total"", ""system.memory.actual.free"",
//    ""system.cpu.total.norm.pct"", ""system.process.cpu.total.norm.pct"",
//    ""system.network.in.bytes"", ""system.network.out.bytes""
//  ],
//  ""query"": {{ ""range"": {{ ""@timestamp"": {{ ""gte"": ""{gteS}"", ""lt"": ""{ltS}"" }} }} }},
//  ""sort"": [ {{ ""@timestamp"": {{ ""order"": ""desc"" }} }} ]
//}}";
//    }

//    private static string BuildApmTxQueryBodyLimited(DateTime gte, DateTime lt, int size)
//    {
//        var gteS = gte.ToString("o", CultureInfo.InvariantCulture);
//        var ltS = lt.ToString("o", CultureInfo.InvariantCulture);
//        return $@"{{
//  ""size"": {size},
//  ""_source"": [""@timestamp"", ""host.name"", ""service.name"", ""event.outcome"", ""transaction.duration.us"", ""transaction.id""],
//  ""query"": {{ ""bool"": {{ ""filter"": [
//     {{ ""term"": {{ ""processor.event"": ""transaction"" }} }},
//     {{ ""range"": {{ ""@timestamp"": {{ ""gte"": ""{gteS}"", ""lt"": ""{ltS}"" }} }} }}
//  ]}}}},
//  ""sort"": [ {{ ""@timestamp"": {{ ""order"": ""desc"" }} }} ]
//}}";
//    }

//    // [APM-NEW] APM metric (CLR/GC)
//    private static string BuildApmClrMetricQueryBodyLimited(DateTime gte, DateTime lt, int size)
//    {
//        var gteS = gte.ToString("o", CultureInfo.InvariantCulture);
//        var ltS = lt.ToString("o", CultureInfo.InvariantCulture);
//        return $@"{{
//  ""size"": {size},
//  ""_source"": [
//    ""@timestamp"", ""host.name"", ""service.name"", ""metricset.name"",
//    ""clr.gc.count"", ""clr.gc.time"",
//    ""clr.gc.gen0size"", ""clr.gc.gen1size"", ""clr.gc.gen2size"", ""clr.gc.gen3size""
//  ],
//  ""query"": {{ ""bool"": {{ ""filter"": [
//     {{ ""term"": {{ ""processor.event"": ""metric"" }} }},
//     {{ ""term"": {{ ""metricset.name"": ""app"" }} }},
//     {{ ""range"": {{ ""@timestamp"": {{ ""gte"": ""{gteS}"", ""lt"": ""{ltS}"" }} }} }}
//  ]}}}},
//  ""sort"": [ {{ ""@timestamp"": {{ ""order"": ""desc"" }} }} ]
//}}";
//    }

//    // [APM-NEW] APM spans (to detect integration failures)
//    private static string BuildApmSpanQueryBodyLimited(DateTime gte, DateTime lt, int size)
//    {
//        var gteS = gte.ToString("o", CultureInfo.InvariantCulture);
//        var ltS = lt.ToString("o", CultureInfo.InvariantCulture);
//        return $@"{{
//  ""size"": {size},
//  ""_source"": [
//    ""@timestamp"", ""host.name"", ""service.name"",
//    ""span.type"", ""span.subtype"", ""span.outcome"",
//    ""destination.service.name"", ""http.response.status_code""
//  ],
//  ""query"": {{ ""bool"": {{ ""filter"": [
//     {{ ""term"": {{ ""processor.event"": ""span"" }} }},
//     {{ ""range"": {{ ""@timestamp"": {{ ""gte"": ""{gteS}"", ""lt"": ""{ltS}"" }} }} }}
//  ]}}}},
//  ""sort"": [ {{ ""@timestamp"": {{ ""order"": ""desc"" }} }} ]
//}}";
//    }

//    // [APM-NEW] APM errors (exceptions)
//    private static string BuildApmErrorQueryBodyLimited(DateTime gte, DateTime lt, int size)
//    {
//        var gteS = gte.ToString("o", CultureInfo.InvariantCulture);
//        var ltS = lt.ToString("o", CultureInfo.InvariantCulture);
//        return $@"{{
//  ""size"": {size},
//  ""_source"": [
//    ""@timestamp"", ""host.name"", ""service.name"",
//    ""error.exception.type"", ""error.exception.message"", ""error.log.message""
//  ],
//  ""query"": {{ ""bool"": {{ ""filter"": [
//     {{ ""term"": {{ ""processor.event"": ""error"" }} }},
//     {{ ""range"": {{ ""@timestamp"": {{ ""gte"": ""{gteS}"", ""lt"": ""{ltS}"" }} }} }}
//  ]}}}},
//  ""sort"": [ {{ ""@timestamp"": {{ ""order"": ""desc"" }} }} ]
//}}";
//    }

//    // -------------------- Build features + APM signals from files --------------------
//    private sealed class Acc
//    {
//        public P2Quantile MemP95 = new(0.95);
//        public P2Quantile CpuP95 = new(0.95);
//        public P2Quantile NetInP95 = new(0.95);
//        public P2Quantile NetOutP95 = new(0.95);
//        public double MemTotalMaxBytes;
//        public double MemUsedMaxBytes;
//        public double CpuMax;         // 0..1
//        public double NetInMaxBytes;
//        public double NetOutMaxBytes;
//        public long TotalDocs;
//        public long IdleDocs;
//        public long OffIdleDocs;
//        public HashSet<string> Hours = new(); // yyyyMMddHH
//    }
//    private sealed class ApmAcc
//    {
//        public long Total;
//        public long Fail;
//        public P2Quantile P95Latency = new(0.95); // microseconds
//        public Dictionary<string, int> ServiceCounts = new(StringComparer.OrdinalIgnoreCase);
//        public Dictionary<string, int> PerMinute = new(); // yyyyMMddHHmm -> count
//    }
//    // [APM-NEW]
//    private sealed class ClrAcc
//    {
//        public Dictionary<string, int> MinuteDocs = new(); // yyyyMMddHHmm -> docs
//        public Dictionary<string, double> MinuteGcCounts = new(); // same key
//        public Dictionary<string, double> MinuteGcTime = new();   // same key
//        public P2Quantile Gen2P95 = new(0.95);
//        public P2Quantile Gen3P95 = new(0.95);
//        public double Gen2Max, Gen3Max;
//    }
//    // [APM-NEW]
//    private sealed class IntAcc
//    {
//        public int FailedHttp, FailedDb, FailedCache;
//        public Dictionary<string, int> ExternalNames = new(StringComparer.OrdinalIgnoreCase);
//        public int ErrorDocs;
//        public Dictionary<string, int> ExType = new(StringComparer.OrdinalIgnoreCase);
//        public Dictionary<string, int> ExMsg = new(StringComparer.OrdinalIgnoreCase);
//    }

//    private static async Task<(List<HostFeatures> features,
//                               Dictionary<string, ApmSignals> apm,
//                               Dictionary<string, ApmClrSignals> apmClr,
//                               Dictionary<string, ApmIntegrationSignals> apmInt)>
//        BuildFeaturesAndApmFromFiles(string metricsDir, string apmTxDir, string apmMetricsDir, string apmSpanDir, string apmErrorDir, ILogger logger)
//    {
//        var acc = new Dictionary<string, Acc>(StringComparer.OrdinalIgnoreCase);

//        // METRICS
//        if (Directory.Exists(metricsDir))
//        {
//            foreach (var file in Directory.EnumerateFiles(metricsDir, "*.ndjson", SearchOption.TopDirectoryOnly))
//            {
//                try
//                {
//                    await foreach (var doc in ReadNdjson(file))
//                    {
//                        if (!doc.TryGetProperty("@timestamp", out var tsProp)) continue;
//                        var ts = tsProp.GetDateTime().ToUniversalTime();
//                        var host = TryString(doc, "host.name") ?? "";
//                        if (string.IsNullOrEmpty(host)) continue;

//                        if (!acc.TryGetValue(host, out var a)) acc[host] = a = new Acc();
//                        a.Hours.Add(ts.ToString("yyyyMMddHH")); a.TotalDocs++;

//                        var memTotal = TryNum(doc, "system.memory.total");
//                        var memFree = TryNum(doc, "system.memory.actual.free");
//                        if (!double.IsNaN(memTotal) && !double.IsNaN(memFree))
//                        {
//                            var used = memTotal - memFree;
//                            a.MemP95.Add(used);
//                            if (memTotal > a.MemTotalMaxBytes) a.MemTotalMaxBytes = memTotal;
//                            if (used > a.MemUsedMaxBytes) a.MemUsedMaxBytes = used;
//                        }

//                        var cpu = TryNum(doc, "system.cpu.total.norm.pct");
//                        if (double.IsNaN(cpu)) cpu = TryNum(doc, "system.process.cpu.total.norm.pct");
//                        if (!double.IsNaN(cpu))
//                        {
//                            a.CpuP95.Add(cpu);
//                            if (cpu > a.CpuMax) a.CpuMax = cpu;
//                            if (cpu < 0.05) a.IdleDocs++;
//                            if (ts.Hour < 7 && cpu < 0.05) a.OffIdleDocs++;
//                        }

//                        var nin = TryNum(doc, "system.network.in.bytes");
//                        var nout = TryNum(doc, "system.network.out.bytes");
//                        if (!double.IsNaN(nin)) { a.NetInP95.Add(nin); if (nin > a.NetInMaxBytes) a.NetInMaxBytes = nin; }
//                        if (!double.IsNaN(nout)) { a.NetOutP95.Add(nout); if (nout > a.NetOutMaxBytes) a.NetOutMaxBytes = nout; }
//                    }
//                }
//                catch (Exception ex) { LogException(logger, $"Parse metrics file {file}", ex); }
//            }
//        }

//        const int expectedHours = 24 * 30; // completeness baseline
//        var features = new List<HostFeatures>(acc.Count);
//        foreach (var (host, a) in acc)
//        {
//            var memP95GiB = (float)((double.IsNaN(a.MemP95.Value) ? 0 : a.MemP95.Value) / Math.Pow(1024, 3));
//            var allocGiB = (float)(a.MemTotalMaxBytes / Math.Pow(1024, 3));
//            var memMaxGiB = (float)(a.MemUsedMaxBytes / Math.Pow(1024, 3));
//            var cpuP95F = (float)(double.IsNaN(a.CpuP95.Value) ? 0 : a.CpuP95.Value);
//            var cpuMaxF = (float)a.CpuMax;
//            var netInKbpsP95 = (float)((double.IsNaN(a.NetInP95.Value) ? 0 : a.NetInP95.Value) * 8 / 1024.0);
//            var netOutKbpsP95 = (float)((double.IsNaN(a.NetOutP95.Value) ? 0 : a.NetOutP95.Value) * 8 / 1024.0);
//            var netInMaxKbps = (float)(a.NetInMaxBytes * 8 / 1024.0);
//            var netOutMaxKbps = (float)(a.NetOutMaxBytes * 8 / 1024.0);
//            var completeness = expectedHours > 0 ? Math.Clamp(a.Hours.Count / (float)expectedHours, 0f, 1f) : 0f;
//            var idleFrac = a.TotalDocs > 0 ? (float)a.IdleDocs / a.TotalDocs : 0f;
//            var offIdleFr = a.TotalDocs > 0 ? (float)a.OffIdleDocs / a.TotalDocs : 0f;

//            features.Add(new HostFeatures
//            {
//                Host = host,
//                MemP95GiB = memP95GiB,
//                CpuP95 = cpuP95F,
//                NetInP95Kbps = netInKbpsP95,
//                NetOutP95Kbps = netOutKbpsP95,
//                RpmP95 = 0,
//                MemHeadroomGiB = Math.Max(0f, allocGiB - memP95GiB),
//                CpuHeadroom = Math.Max(0f, 1f - cpuP95F),
//                IdleFraction = idleFrac,
//                OffhoursIdleFraction = offIdleFr,
//                DataCompleteness = completeness,
//                AllocMemGiB = allocGiB,
//                CpuMax = cpuMaxF,
//                MemUsedMaxGiB = memMaxGiB,
//                NetInMaxKbps = netInMaxKbps,
//                NetOutMaxKbps = netOutMaxKbps,
//                HoursObserved = a.Hours.Count,
//                TotalDocs = a.TotalDocs
//            });
//        }

//        // APM txn
//        var apm = new Dictionary<string, ApmSignals>(StringComparer.OrdinalIgnoreCase);
//        if (Directory.Exists(apmTxDir))
//        {
//            var map = new Dictionary<string, ApmAcc>(StringComparer.OrdinalIgnoreCase);
//            foreach (var file in Directory.EnumerateFiles(apmTxDir, "*.ndjson", SearchOption.TopDirectoryOnly))
//            {
//                try
//                {
//                    await foreach (var doc in ReadNdjson(file))
//                    {
//                        var host = TryString(doc, "host.name") ?? "";
//                        if (string.IsNullOrEmpty(host)) continue;
//                        if (!map.TryGetValue(host, out var a)) map[host] = a = new ApmAcc();

//                        a.Total++;
//                        var outcome = TryString(doc, "event.outcome");
//                        if (string.Equals(outcome, "failure", StringComparison.OrdinalIgnoreCase)) a.Fail++;

//                        var durUs = TryNum(doc, "transaction.duration.us");
//                        if (!double.IsNaN(durUs)) a.P95Latency.Add(durUs);

//                        var svc = TryString(doc, "service.name");
//                        if (!string.IsNullOrEmpty(svc))
//                            a.ServiceCounts[svc] = a.ServiceCounts.TryGetValue(svc, out var c) ? c + 1 : 1;

//                        if (doc.TryGetProperty("@timestamp", out var tsProp))
//                        {
//                            var key = tsProp.GetDateTime().ToUniversalTime().ToString("yyyyMMddHHmm");
//                            a.PerMinute[key] = a.PerMinute.TryGetValue(key, out var c) ? c + 1 : 1;
//                        }
//                    }
//                }
//                catch (Exception ex) { LogException(logger, $"APM txn file {file}", ex); }
//            }
//            foreach (var (host, a) in map)
//            {
//                var errRate = a.Total > 0 ? (float)a.Fail / a.Total : 0f;
//                var p95ms = (float)(double.IsNaN(a.P95Latency.Value) ? 0 : a.P95Latency.Value / 1000.0);
//                var rpmQ = new P2Quantile(0.95); foreach (var v in a.PerMinute.Values) rpmQ.Add(v);
//                var rpm95 = (float)(double.IsNaN(rpmQ.Value) ? 0 : rpmQ.Value);
//                string? topService = a.ServiceCounts.Count == 0 ? null : a.ServiceCounts.OrderByDescending(k => k.Value).First().Key;
//                apm[host] = new ApmSignals { Host = host, ErrorRate = errRate, LatencyP95Ms = p95ms, ApproxRpmP95 = rpm95, TopService = topService };
//            }
//            foreach (var hf in features)
//                if (apm.TryGetValue(hf.Host, out var sig)) hf.RpmP95 = Math.Max(hf.RpmP95, sig.ApproxRpmP95);
//        }

//        // [APM-NEW] CLR metrics
//        var clr = new Dictionary<string, ApmClrSignals>(StringComparer.OrdinalIgnoreCase);
//        if (Directory.Exists(apmMetricsDir))
//        {
//            var map = new Dictionary<string, ClrAcc>(StringComparer.OrdinalIgnoreCase);
//            foreach (var file in Directory.EnumerateFiles(apmMetricsDir, "*.ndjson", SearchOption.TopDirectoryOnly))
//            {
//                try
//                {
//                    await foreach (var doc in ReadNdjson(file))
//                    {
//                        var host = TryString(doc, "host.name") ?? "";
//                        if (string.IsNullOrEmpty(host)) continue;
//                        if (!map.TryGetValue(host, out var a)) map[host] = a = new ClrAcc();

//                        var ts = doc.GetProperty("@timestamp").GetDateTime().ToUniversalTime();
//                        var key = ts.ToString("yyyyMMddHHmm");

//                        double gcCount = TryNum(doc, "clr.gc.count");
//                        double gcTime = TryNum(doc, "clr.gc.time");
//                        if (!double.IsNaN(gcCount))
//                        {
//                            a.MinuteDocs[key] = a.MinuteDocs.TryGetValue(key, out var n) ? n + 1 : 1;
//                            a.MinuteGcCounts[key] = a.MinuteGcCounts.TryGetValue(key, out var v) ? v + gcCount : gcCount;
//                        }
//                        if (!double.IsNaN(gcTime))
//                            a.MinuteGcTime[key] = a.MinuteGcTime.TryGetValue(key, out var v2) ? v2 + gcTime : gcTime;

//                        void addP(P2Quantile q, ref double mx, string field)
//                        {
//                            var val = TryNum(doc, field);
//                            if (!double.IsNaN(val)) { q.Add(val); if (val > mx) mx = val; }
//                        }
//                        addP(a.Gen2P95, ref a.Gen2Max, "clr.gc.gen2size");
//                        addP(a.Gen3P95, ref a.Gen3Max, "clr.gc.gen3size");
//                    }
//                }
//                catch (Exception ex) { LogException(logger, $"APM metrics file {file}", ex); }
//            }

//            foreach (var (host, a) in map)
//            {
//                // crude per-minute p95 of counts/time
//                var gcCountQ = new P2Quantile(0.95);
//                var gcTimePctQ = new P2Quantile(0.95);
//                foreach (var key in a.MinuteDocs.Keys)
//                {
//                    var n = a.MinuteDocs[key];
//                    var count = a.MinuteGcCounts.TryGetValue(key, out var c) ? c : 0d;
//                    var time = a.MinuteGcTime.TryGetValue(key, out var t) ? t : 0d;
//                    gcCountQ.Add(count / Math.Max(1, n)); // average per doc in that minute
//                    // GC time unit unknown in your payload; treat as "milliseconds per sample", normalize to ~% guess
//                    // Here we just map to [0..100] crudely:
//                    gcTimePctQ.Add(Math.Min(100.0, time));
//                }

//                var gen2p95GiB = (float)((double.IsNaN(a.Gen2P95.Value) ? 0 : a.Gen2P95.Value) / (1024.0 * 1024.0 * 1024.0));
//                var gen3p95GiB = (float)((double.IsNaN(a.Gen3P95.Value) ? 0 : a.Gen3P95.Value) / (1024.0 * 1024.0 * 1024.0));
//                var gen2maxGiB = (float)(a.Gen2Max / (1024.0 * 1024.0 * 1024.0));
//                var gen3maxGiB = (float)(a.Gen3Max / (1024.0 * 1024.0 * 1024.0));
//                var countP95 = (float)(double.IsNaN(gcCountQ.Value) ? 0 : gcCountQ.Value);
//                var timePctP95 = (float)(double.IsNaN(gcTimePctQ.Value) ? 0 : gcTimePctQ.Value);

//                clr[host] = new ApmClrSignals
//                {
//                    Host = host,
//                    GcCountPerMinP95 = countP95,
//                    GcTimePctP95 = timePctP95,
//                    Gen2SizeP95GiB = gen2p95GiB,
//                    Gen3SizeP95GiB = gen3p95GiB,
//                    Gen2SizeMaxGiB = gen2maxGiB,
//                    Gen3SizeMaxGiB = gen3maxGiB
//                };
//            }
//        }

//        // [APM-NEW] spans/errors (integrations)
//        var integ = new Dictionary<string, ApmIntegrationSignals>(StringComparer.OrdinalIgnoreCase);
//        // spans
//        if (Directory.Exists(apmSpanDir))
//        {
//            var map = new Dictionary<string, IntAcc>(StringComparer.OrdinalIgnoreCase);
//            foreach (var file in Directory.EnumerateFiles(apmSpanDir, "*.ndjson", SearchOption.TopDirectoryOnly))
//            {
//                try
//                {
//                    await foreach (var doc in ReadNdjson(file))
//                    {
//                        var host = TryString(doc, "host.name") ?? "";
//                        if (string.IsNullOrEmpty(host)) continue;
//                        if (!map.TryGetValue(host, out var a)) map[host] = a = new IntAcc();

//                        var outcome = TryString(doc, "span.outcome") ?? "";
//                        var type = (TryString(doc, "span.type") ?? "").ToLowerInvariant();
//                        var subtype = (TryString(doc, "span.subtype") ?? "").ToLowerInvariant();
//                        var ext = TryString(doc, "destination.service.name");
//                        if (!string.IsNullOrEmpty(ext)) a.ExternalNames[ext] = a.ExternalNames.TryGetValue(ext, out var c) ? c + 1 : 1;

//                        bool failed = string.Equals(outcome, "failure", StringComparison.OrdinalIgnoreCase);
//                        if (failed)
//                        {
//                            if (type == "external" || subtype == "http") a.FailedHttp++;
//                            else if (type == "db" || subtype.Contains("sql") || subtype.Contains("mongo")) a.FailedDb++;
//                            else if (subtype.Contains("cache") || subtype.Contains("redis")) a.FailedCache++;
//                        }
//                    }
//                }
//                catch (Exception ex) { LogException(logger, $"APM span file {file}", ex); }
//            }

//            foreach (var (host, a) in map)
//            {
//                string? topExt = a.ExternalNames.Count == 0 ? null : a.ExternalNames.OrderByDescending(k => k.Value).First().Key;
//                integ[host] = new ApmIntegrationSignals { Host = host, FailedHttpSpans = a.FailedHttp, FailedDbSpans = a.FailedDb, FailedCacheSpans = a.FailedCache, TopExternalService = topExt };
//            }
//        }
//        // errors
//        if (Directory.Exists(apmErrorDir))
//        {
//            var map = integ; // enrich the same map
//            foreach (var file in Directory.EnumerateFiles(apmErrorDir, "*.ndjson", SearchOption.TopDirectoryOnly))
//            {
//                try
//                {
//                    await foreach (var doc in ReadNdjson(file))
//                    {
//                        var host = TryString(doc, "host.name") ?? "";
//                        if (string.IsNullOrEmpty(host)) continue;
//                        if (!map.TryGetValue(host, out var s)) map[host] = s = new ApmIntegrationSignals { Host = host };

//                        s.TotalErrorDocs++;
//                        var type = TryString(doc, "error.exception.type") ?? "";
//                        var msg = TryString(doc, "error.exception.message") ?? (TryString(doc, "error.log.message") ?? "");

//                        // We’ll keep only the most frequent via temp dictionaries; simpler: store first if null
//                        if (string.IsNullOrEmpty(s.TopExceptionType) && !string.IsNullOrEmpty(type)) s.TopExceptionType = type;
//                        if (string.IsNullOrEmpty(s.TopExceptionMessage) && !string.IsNullOrEmpty(msg)) s.TopExceptionMessage = msg;
//                    }
//                }
//                catch (Exception ex) { LogException(logger, $"APM error file {file}", ex); }
//            }
//        }

//        return (features, apm, clr, integ);
//    }

//    private static async Task<float[]> GetHourlyCpuSeriesForHostFromFiles(string metricsDir, string host, int days, ILogger logger)
//    {
//        var from = DateTime.UtcNow.AddDays(-days);
//        var perHour = new SortedDictionary<string, (double sum, int n)>();
//        if (!Directory.Exists(metricsDir)) return Array.Empty<float>();

//        foreach (var file in Directory.EnumerateFiles(metricsDir, "*.ndjson", SearchOption.TopDirectoryOnly))
//        {
//            try
//            {
//                await foreach (var doc in ReadNdjson(file))
//                {
//                    if (!doc.TryGetProperty("@timestamp", out var tsProp)) continue;
//                    var ts = tsProp.GetDateTime().ToUniversalTime();
//                    if (ts < from) continue;

//                    if ((TryString(doc, "host.name") ?? "") != host) continue;
//                    var hourKey = ts.ToString("yyyyMMddHH");
//                    var cpu = TryNum(doc, "system.cpu.total.norm.pct");
//                    if (double.IsNaN(cpu)) cpu = TryNum(doc, "system.process.cpu.total.norm.pct");
//                    if (double.IsNaN(cpu)) continue;

//                    var tup = perHour.TryGetValue(hourKey, out var v) ? v : (0, 0);
//                    tup.sum += cpu; tup.n++;
//                    perHour[hourKey] = tup;
//                }
//            }
//            catch (Exception ex) { LogException(logger, $"Trend file {file}", ex); }
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
//            if (first.TryGetProperty(parts[1], out var vv) && vv.ValueKind == JsonValueKind.Number) return vv.GetDouble();
//        return double.NaN;
//    }
//    private static string? TryString(JsonElement root, string dottedPath)
//    {
//        if (root.TryGetProperty(dottedPath, out var v) && v.ValueKind == JsonValueKind.String) return v.GetString();
//        var parts = dottedPath.Split('.', 2);
//        if (parts.Length == 2 && root.TryGetProperty(parts[0], out var first) && first.ValueKind == JsonValueKind.Object)
//            if (first.TryGetProperty(parts[1], out var vv) && vv.ValueKind == JsonValueKind.String) return vv.GetString();
//        return null;
//    }


//    private static string MapCpuToTierDown(float cpuP95) =>
//        cpuP95 switch
//        {
//            < 0.05f => "XXS (0.5 vCPU)",
//            < 0.10f => "XS (1 vCPU)",
//            < 0.20f => "S (2 vCPU)",
//            < 0.35f => "M (4 vCPU)",
//            < 0.60f => "L (8 vCPU)",
//            _ => "Keep current"
//        };
//}
