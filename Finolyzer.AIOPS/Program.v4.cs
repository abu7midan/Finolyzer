//// Program.cs
//// .NET 9 Minimal API + ML.NET (PCA anomaly + KMeans clustering + trend guard)
//// EXPORT (per-hour, last 30d): fetch TOP 100 docs **per hour window** for metrics & APM
//// TRAIN from exported NDJSON files, SUGGEST from files
//// Verbose console logging for every step.
////
//// Endpoints:
////   GET  /health
////   POST /train            -> export per-hour (size=100) for last 30d, train models
////   GET  /suggestions      -> load models, rebuild features from NDJSON, return suggestions
////
//// Required NuGet:
////   Microsoft.ML
////   Microsoft.ML.Mkl.Components (optional)
////
//// NOTE: You asked to keep these usings explicitly:
//using Microsoft.ML;
//using Microsoft.ML.Data;
//using Microsoft.ML.Transforms;
//using System.Text;
//using System.Text.Json;

//// (Additional needed usings)
//using System.Globalization;
//using System.Net.Http.Headers;

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
//    public float DataCompleteness { get; set; }   // non-empty hours / 720 (approx)
//    public float AllocMemGiB { get; set; }
//}

//// APM signals per host (for richer suggestions; filled from files)
//public sealed class ApmSignals
//{
//    public string Host { get; set; } = "";
//    public float ErrorRate { get; set; }          // failures / total
//    public float LatencyP95Ms { get; set; }       // ms
//    public float ApproxRpmP95 { get; set; }       // from minute buckets in sample
//    public string? TopService { get; set; }
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
//// Carries the 'Features' vector into the custom mapping
//public sealed class AnomalyFeaturesInput
//{
//    public VBuffer<float> Features { get; set; } // vector after Concatenate
//}

//public static class ModelTrainer
//{
//    public static (ITransformer anomalyModel, ITransformer clusterModel) Train(
//        MLContext ml, List<HostFeatures> rows, string modelDir, ILogger logger)
//    {
//        var clean = SanitizeRows(rows, logger);
//        var rowCount = clean.Count;
//        var featureCount = Columns.AnomalyCols.Length;

//        if (rowCount < 2)
//        {
//            logger.LogWarning("ModelTrainer: Too few rows after sanitization: {Rows}. Using dummy models.", rowCount);
//            var dummyAnom = BuildDummyAnomalyModel(ml, clean, logger);
//            var dummyClus = BuildDummyClusterModel(ml, clean, logger);
//            SaveBoth(ml, dummyAnom, dummyClus, clean, modelDir, logger);
//            return (dummyAnom, dummyClus);
//        }

//        logger.LogInformation("ModelTrainer: Loading {RowCount} sanitized rows into IDataView...", rowCount);
//        var data = ml.Data.LoadFromEnumerable(clean);

//        // ----------- Robust anomaly pipeline (no PCA) -----------
//        var anomalyPairs = Columns.AnomalyCols.Select(c => new InputOutputColumnPair(c, c)).ToArray();

//        // NormalizeMeanVariance learns mean/std at fit-time. We then compute an L2 norm over standardized features.
//        var anomalyPipeline =
//            ml.Transforms.ReplaceMissingValues(anomalyPairs, MissingValueReplacingEstimator.ReplacementMode.Mean)
//              .Append(ml.Transforms.NormalizeMeanVariance(anomalyPairs)) // -> standardized columns
//              .Append(ml.Transforms.Concatenate("Features", Columns.AnomalyCols))
//              .Append(ml.Transforms.CustomMapping<AnomalyFeaturesInput, AnomalyOutput>(
//                  (inp, outp) =>
//                  {
//                      // L2 norm of standardized vector, scaled by sqrt(dim)
//                      var values = inp.Features.GetValues(); // dense or sparse is fine
//                      double sumSq = 0;
//                      for (int i = 0; i < values.Length; i++)
//                          sumSq += (double)values[i] * values[i];

//                      var dim = Math.Max(1, values.Length);
//                      var l2 = Math.Sqrt(sumSq) / Math.Sqrt(dim);

//                      // Map to [0..1] with a soft squashing (optional, keeps scale compact)
//                      // score ~= l2 / (1 + l2)
//                      var score = (float)(l2 / (1.0 + l2));

//                      outp.PredictedLabel = false;   // we don't threshold here
//                      outp.Score = score;            // used by your Confidence() blend
//                      outp.PValue = 1f - score;      // rough complement
//                  },
//                  contractName: "L2ZScoreAnomaly"));

//        logger.LogInformation("ModelTrainer: Fitting anomaly (z-score L2)...");
//        ITransformer anomalyModel = anomalyPipeline.Fit(data);

//        // ----------- Adaptive KMeans with safe fallback -----------
//        var clusterModel = FitCluster(ml, clean, logger);

//        // Save
//        SaveBoth(ml, anomalyModel, clusterModel, clean, modelDir, logger);
//        logger.LogInformation("ModelTrainer: Done.");
//        return (anomalyModel, clusterModel);
//    }

//    private static void SaveBoth(MLContext ml, ITransformer a, ITransformer c, List<HostFeatures> rows, string modelDir, ILogger logger)
//    {
//        Directory.CreateDirectory(modelDir);
//        var anomalyPath = Path.Combine(modelDir, "anomaly.zip");
//        var clusterPath = Path.Combine(modelDir, "cluster.zip");
//        var schema = ml.Data.LoadFromEnumerable(rows).Schema;

//        logger.LogInformation("ModelTrainer: Saving models -> {AnomalyPath}, {ClusterPath}", anomalyPath, clusterPath);
//        ml.Model.Save(a, schema, anomalyPath);
//        ml.Model.Save(c, schema, clusterPath);
//    }

//    // ======= Clustering (same adaptive/fallback you’re using) =======
//    private static ITransformer FitCluster(MLContext ml, List<HostFeatures> rows, ILogger logger)
//    {
//        var uniq = CountUniqueByClusterCols(rows);
//        logger.LogInformation("ModelTrainer: clustering candidates rows={Rows}, unique={Unique}", rows.Count, uniq);

//        if (rows.Count < 3 || uniq < 2)
//        {
//            logger.LogWarning("ModelTrainer: Not enough unique examples for KMeans. Using dummy cluster model.");
//            return BuildDummyClusterModel(ml, rows, logger);
//        }

//        var k = Math.Min(8, Math.Max(2, uniq));
//        k = Math.Min(k, rows.Count - 1);

//        var data = ml.Data.LoadFromEnumerable(rows);
//        var clusterPairs = Columns.ClusterCols.Select(c => new InputOutputColumnPair(c, c)).ToArray();

//        var pipeline =
//            ml.Transforms.ReplaceMissingValues(clusterPairs, MissingValueReplacingEstimator.ReplacementMode.Mean)
//              .Append(ml.Transforms.NormalizeMinMax(clusterPairs))
//              .Append(ml.Transforms.Concatenate("Features", Columns.ClusterCols))
//              .Append(ml.Clustering.Trainers.KMeans(featureColumnName: "Features", numberOfClusters: k));

//        try
//        {
//            logger.LogInformation("ModelTrainer: Fitting KMeans (k={K})...", k);
//            return pipeline.Fit(data);
//        }
//        catch (Exception ex)
//        {
//            logger.LogWarning(ex, "ModelTrainer: KMeans failed with k={K}. Falling back to dummy cluster model.", k);
//            return BuildDummyClusterModel(ml, rows, logger);
//        }
//    }

//    private static int CountUniqueByClusterCols(List<HostFeatures> rows)
//    {
//        static string Sig(HostFeatures r) => string.Join("|", new[]
//        {
//            r.CpuP95.ToString("G9"),
//            r.MemP95GiB.ToString("G9"),
//            r.RpmP95.ToString("G9"),
//            r.NetInP95Kbps.ToString("G9"),
//            r.NetOutP95Kbps.ToString("G9")
//        });
//        return rows.Select(Sig).Distinct().Count();
//    }

//    private static ITransformer BuildDummyClusterModel(MLContext ml, List<HostFeatures> rows, ILogger logger)
//    {
//        logger.LogInformation("ModelTrainer: Building dummy cluster transformer (ClusterId=1).");
//        var data = ml.Data.LoadFromEnumerable(rows);

//        var pipe = ml.Transforms.CustomMapping<HostFeatures, ClusterOutput>(
//            (input, output) =>
//            {
//                output.ClusterId = 1;
//                output.Distance = null;
//            },
//            contractName: "DummyCluster");

//        return pipe.Fit(data);
//    }

//    private static ITransformer BuildDummyAnomalyModel(MLContext ml, List<HostFeatures> rows, ILogger logger)
//    {
//        logger.LogInformation("ModelTrainer: Building dummy anomaly transformer (constant Score=0.5, PValue=0.5)...");
//        var data = ml.Data.LoadFromEnumerable(rows);

//        var pipe = ml.Transforms.CustomMapping<HostFeatures, AnomalyOutput>(
//            (input, output) =>
//            {
//                output.PredictedLabel = false;
//                output.Score = 0.5f;
//                output.PValue = 0.5f;
//            },
//            contractName: "DummyAnomaly");

//        return pipe.Fit(data);
//    }

//    // ---------- hygiene ----------

//    private static List<HostFeatures> SanitizeRows(List<HostFeatures> rows, ILogger logger)
//    {
//        static float Clamp(double v, double min, double max)
//        {
//            if (double.IsNaN(v) || double.IsInfinity(v)) v = 0d;
//            if (v < min) v = min; else if (v > max) v = max;
//            return (float)v;
//        }

//        int before = rows.Count;
//        var clean = rows.Where(r =>
//            !string.IsNullOrWhiteSpace(r.Host) &&
//            float.IsFinite(r.MemP95GiB) && float.IsFinite(r.CpuP95) &&
//            float.IsFinite(r.NetInP95Kbps) && float.IsFinite(r.NetOutP95Kbps) &&
//            float.IsFinite(r.RpmP95) &&
//            float.IsFinite(r.MemHeadroomGiB) && float.IsFinite(r.CpuHeadroom) &&
//            float.IsFinite(r.IdleFraction) && float.IsFinite(r.OffhoursIdleFraction) &&
//            float.IsFinite(r.DataCompleteness) && float.IsFinite(r.AllocMemGiB)
//        ).Select(r => new HostFeatures
//        {
//            Host = r.Host,
//            MemP95GiB = Clamp(r.MemP95GiB, 0, 1e6),
//            CpuP95 = Clamp(r.CpuP95, 0, 1),
//            NetInP95Kbps = Clamp(r.NetInP95Kbps, 0, 1e9),
//            NetOutP95Kbps = Clamp(r.NetOutP95Kbps, 0, 1e9),
//            RpmP95 = Clamp(r.RpmP95, 0, 1e9),
//            MemHeadroomGiB = Clamp(r.MemHeadroomGiB, 0, 1e6),
//            CpuHeadroom = Clamp(r.CpuHeadroom, 0, 1),
//            IdleFraction = Clamp(r.IdleFraction, 0, 1),
//            OffhoursIdleFraction = Clamp(r.OffhoursIdleFraction, 0, 1),
//            DataCompleteness = Clamp(r.DataCompleteness, 0, 1),
//            AllocMemGiB = Clamp(r.AllocMemGiB, 0, 1e6)
//        }).ToList();

//        int after = clean.Count;
//        if (after < before)
//            logger.LogWarning("ModelTrainer: Dropped {Dropped} rows due to NaN/Inf/out-of-range.", before - after);

//        return clean;
//    }
//}


//// -------------------- Trend guard (slope on hourly CPU) --------------------

//public static class TrendGuard
//{
//    public static bool IsTrendingUp(float[] cpuSeries, int minPoints = 24 * 7, double slopeThreshold = 0.0007)
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
//    internal static string MetricsIndex => Environment.GetEnvironmentVariable("MB_INDEX") ?? ".ds-metricbeat-*,metricbeat-*";
//    internal static string ApmTxIndex => Environment.GetEnvironmentVariable("APM_TX_INDEX") ?? "traces-apm*,apm-*";

//    // Export destination
//    internal static string MetricsOutDir => "./data/metrics";
//    internal static string ApmOutDir => "./data/apm";

//    // APM thresholds (tweak as you like)
//    internal const float ErrorRateWarn = 0.02f;  // 2%
//    internal const float LatencyP95WarnMs = 1200f;  // 1.2s
//    internal const float RpmPresentThreshold = 1f;     // minimal traffic presence
//    internal const float CpuHighThreshold = 0.75f;  // 75%
//    internal const float MemHeadroomLowGiB = 0.5f;   // < 0.5 GiB headroom

//    // Per-hour cap
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

//        // ======== /train: per-hour export (size=100 per hour) for last 30d, then train ========
//        app.MapPost("/train", async () =>
//        {
//            try
//            {
//                var toUtc = DateTime.UtcNow;
//                var fromUtc = toUtc.AddDays(-30);
//                logger.LogInformation("POST /train: Per-hour limited export size={Size} for {From} -> {To}", PerHourSize, fromUtc, toUtc);

//                Directory.CreateDirectory(MetricsOutDir);
//                Directory.CreateDirectory(ApmOutDir);
//                if (false)
//                {
//                    // Metrics: per-hour files
//                    await ExportPerHourLimitedNdjson(
//                        MetricsIndex,
//                        fromUtc,
//                        toUtc,
//                        MetricsOutDir,
//                        BuildMetricsQueryBodyLimited,
//                        PerHourSize,
//                        logger);

//                    // APM: per-hour files
//                    await ExportPerHourLimitedNdjson(
//                        ApmTxIndex,
//                        fromUtc,
//                        toUtc,
//                        ApmOutDir,
//                        BuildApmTxQueryBodyLimited,
//                        PerHourSize,
//                        logger);
//                }
//                logger.LogInformation("POST /train: Building features from per-hour files...");
//                var ml = new MLContext(seed: 42);
//                var (features, apmSignals) = await BuildFeaturesAndApmFromFiles(MetricsOutDir, ApmOutDir, logger);
//                logger.LogInformation("POST /train: Features hosts={Hosts}, APM hosts={ApmHosts}", features.Count, apmSignals.Count);

//                if (features.Count == 0)
//                {
//                    logger.LogWarning("POST /train: No features built. Training aborted.");
//                    return Results.Ok(new { trained = 0, note = "No features from files." });
//                }

//                logger.LogInformation("POST /train: Training models...");
//                var (anomaly, cluster) = ModelTrainer.Train(ml, features, "models", logger);
//                logger.LogInformation("POST /train: Training done.");
//                return Results.Ok(new { trained = features.Count, when = DateTime.UtcNow });
//            }
//            catch (Exception ex)
//            {
//                LogException(logger, "POST /train failed", ex);
//                return Results.Problem(ex.Message);
//            }
//        });

//        // ======== /suggestions: build features + apm from files, load models, score ========
//        app.MapGet("/suggestions", async () =>
//        {
//            try
//            {
//                logger.LogInformation("GET /suggestions: Loading features+APM from files...");
//                var ml = new MLContext();

//                var (features, apmSignals) = await BuildFeaturesAndApmFromFiles(MetricsOutDir, ApmOutDir, logger);
//                logger.LogInformation("GET /suggestions: Loaded features for {Count} hosts; APM signals for {ApmHosts} hosts.", features.Count, apmSignals.Count);

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
//                        apmSignals.TryGetValue(h.Host, out var apm);

//                        var ao = anomalyEngine.Predict(h);
//                        var co = clusterEngine.Predict(h);

//                        var cpuSeries = await GetHourlyCpuSeriesForHostFromFiles(MetricsOutDir, h.Host, 7, logger);
//                        var trendingUp = TrendGuard.IsTrendingUp(cpuSeries);
//                        var suggs = new List<object>();

//                        // Scale-out
//                        if ((h.CpuP95 >= CpuHighThreshold && (apm?.ApproxRpmP95 ?? h.RpmP95) >= RpmPresentThreshold) ||
//                            ((apm?.LatencyP95Ms ?? 0) >= LatencyP95WarnMs && (apm?.ApproxRpmP95 ?? h.RpmP95) >= RpmPresentThreshold && (apm?.ErrorRate ?? 0) < 0.01))
//                        {
//                            var reason = h.CpuP95 >= CpuHighThreshold ? $"CPU p95 {h.CpuP95:P0}" : $"Latency p95 {(apm?.LatencyP95Ms ?? 0):0} ms";
//                            suggs.Add(new
//                            {
//                                type = "scale_out",
//                                message = $"{h.Host}: consider scaling out (add pod/node). Reason: {reason} with traffic ~{(apm?.ApproxRpmP95 ?? h.RpmP95):0} rpm.",
//                                confidence = 0.72
//                            });
//                        }

//                        // Increase memory
//                        if (h.MemHeadroomGiB <= MemHeadroomLowGiB && h.MemP95GiB > 0 && h.AllocMemGiB > 0 && (h.MemP95GiB / Math.Max(h.AllocMemGiB, 0.01f)) >= 0.85f)
//                        {
//                            var target = Math.Max((int)Math.Ceiling(h.MemP95GiB * 1.25), (int)Math.Ceiling(h.AllocMemGiB));
//                            suggs.Add(new
//                            {
//                                type = "increase_memory",
//                                message = $"{h.Host}: memory near saturation (p95 {h.MemP95GiB:0.0} GiB / alloc {h.AllocMemGiB:0.0} GiB, headroom {h.MemHeadroomGiB:0.0} GiB). Consider ≈ {target} GiB.",
//                                confidence = 0.7
//                            });
//                        }

//                        // APM error rate
//                        if (apm is not null && apm.ErrorRate >= ErrorRateWarn && apm.ApproxRpmP95 >= RpmPresentThreshold)
//                        {
//                            suggs.Add(new
//                            {
//                                type = "apm_error_rate",
//                                message = $"{h.Host}: high error rate {apm.ErrorRate:P1} (service: {apm.TopService ?? "unknown"}). Investigate exceptions and failing endpoints.",
//                                confidence = 0.8
//                            });
//                        }

//                        // APM latency
//                        if (apm is not null && apm.LatencyP95Ms >= LatencyP95WarnMs && apm.ApproxRpmP95 >= RpmPresentThreshold)
//                        {
//                            suggs.Add(new
//                            {
//                                type = "apm_high_latency",
//                                message = $"{h.Host}: p95 latency {(apm.LatencyP95Ms):0} ms under load ~{apm.ApproxRpmP95:0} rpm. Check DB/IO, N+1 queries, caching, or scale-out.",
//                                confidence = 0.68
//                            });
//                        }

//                        // Under-utilization rightsizing (ML)
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

//                        // Off-hours shutdown
//                        if (h.OffhoursIdleFraction >= 0.8f && !trendingUp)
//                        {
//                            suggs.Add(new
//                            {
//                                type = "offhours_shutdown",
//                                message = $"{h.Host}: off-hours idle {h.OffhoursIdleFraction:P0} → schedule stop 00:00–07:00.",
//                                confidence = 0.65
//                            });
//                        }

//                        // Conservative shutdown candidate
//                        if (h.RpmP95 < 1 && h.CpuP95 < 0.05f && (h.NetInP95Kbps + h.NetOutP95Kbps) < 10)
//                        {
//                            suggs.Add(new
//                            {
//                                type = "shutdown_candidate",
//                                message = $"{h.Host}: near-zero traffic and CPU; candidate to turn off / consolidate.",
//                                confidence = 0.6
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

//    // -------------------- Elasticsearch: PER-HOUR limited fetch (size=N per hour, no scroll) --------------------

//    private static HttpClient CreateEsHttp(ILogger logger)
//    {
//        logger.LogInformation("Creating HttpClient for ES: {Url}", EsUrl);
//        var http = new HttpClient { BaseAddress = new Uri(EsUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(3) };
//        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", EsApiKeyBase64);
//        return http;
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
//        logger.LogInformation("ExportPerHour: index='{Index}', outDir='{OutDir}', window {From} -> {To}, size/hour={Size}", indexPattern, outDir, fromUtc, toUtc, size);

//        using var http = CreateEsHttp(logger);
//        int hoursExported = 0, filesSkipped = 0, totalLines = 0;

//        for (var windowStart = fromUtc; windowStart < toUtc; windowStart = windowStart.AddHours(1))
//        {
//            var windowEnd = windowStart.AddHours(1);
//            var fileName = Path.Combine(outDir, $"{SanitizeIndex(indexPattern)}_{windowStart:yyyyMMdd_HH}.ndjson");

//            if (File.Exists(fileName))
//            {
//                filesSkipped++;
//                logger.LogInformation("ExportPerHour: skip existing file {File}", fileName);
//                continue;
//            }

//            try
//            {
//                var body = buildLimitedBody(windowStart, windowEnd, size);
//                var url = $"{indexPattern}/_search";

//                logger.LogInformation("ExportPerHour: {Start:o}..{End:o} -> {File}; POST {Url}", windowStart, windowEnd, fileName, url);
//                using var req = new HttpRequestMessage(HttpMethod.Post, url)
//                {
//                    Content = new StringContent(body, Encoding.UTF8, "application/json")
//                };
//                using var res = await http.SendAsync(req);
//                res.EnsureSuccessStatusCode();
//                using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());

//                int lines = 0;
//                await using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
//                using var writer = new StreamWriter(fs, new UTF8Encoding(false));

//                if (doc.RootElement.TryGetProperty("hits", out var hits) &&
//                    hits.TryGetProperty("hits", out var arr) &&
//                    arr.ValueKind == JsonValueKind.Array)
//                {
//                    foreach (var h in arr.EnumerateArray())
//                    {
//                        if (h.TryGetProperty("_source", out var src))
//                        {
//                            await writer.WriteLineAsync(src.GetRawText());
//                            lines++;
//                        }
//                    }
//                }

//                hoursExported++;
//                totalLines += lines;
//                logger.LogInformation("ExportPerHour: wrote {Lines} lines -> {File}", lines, fileName);
//            }
//            catch (Exception exHour)
//            {
//                LogException(logger, $"ExportPerHour failed for hour {windowStart:yyyy-MM-dd HH}:00", exHour);
//                // continue with next hour
//            }
//        }

//        logger.LogInformation("ExportPerHour summary: hoursExported={Hours}, filesSkipped={Skipped}, totalLines={Lines}", hoursExported, filesSkipped, totalLines);

//        static string SanitizeIndex(string idx) => string.Concat(idx.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')).Trim('_');
//    }

//    // Pull only fields we use; SORT newest first inside the hour and cap to {size}
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
//  ""_source"": [""@timestamp"", ""host.name"", ""service.name"", ""event.outcome"", ""transaction.duration.us"", ""transaction.id""],
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

//    // -------------------- Build features + APM signals from files (logging) --------------------

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

//    private sealed class ApmAcc
//    {
//        public long Total;
//        public long Fail;
//        public P2Quantile P95Latency = new(0.95); // microseconds
//        public Dictionary<string, int> ServiceCounts = new(StringComparer.OrdinalIgnoreCase);
//        public Dictionary<string, int> PerMinute = new(); // yyyyMMddHHmm -> count
//    }

//    private static async Task<(List<HostFeatures> features, Dictionary<string, ApmSignals> apm)>
//        BuildFeaturesAndApmFromFiles(string metricsDir, string? apmDir, ILogger logger)
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

//        // Build HostFeatures
//        const int expectedHours = 24 * 30; // for completeness
//        var outList = new List<HostFeatures>(acc.Count);

//        foreach (var (host, a) in acc)
//        {
//            var memP95GiB = (float)((double.IsNaN(a.MemP95.Value) ? 0 : a.MemP95.Value) / Math.Pow(1024, 3));
//            var allocGiB = (float)(a.MemTotalMaxBytes / Math.Pow(1024, 3));
//            var cpuP95F = (float)(double.IsNaN(a.CpuP95.Value) ? 0 : a.CpuP95.Value);
//            var netInKbps = (float)((double.IsNaN(a.NetInP95.Value) ? 0 : a.NetInP95.Value) * 8 / 1024.0);
//            var netOutKbps = (float)((double.IsNaN(a.NetOutP95.Value) ? 0 : a.NetOutP95.Value) * 8 / 1024.0);

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
//                RpmP95 = 0, // will be filled from APM proxy if available
//                MemHeadroomGiB = Math.Max(0f, allocGiB - memP95GiB),
//                CpuHeadroom = Math.Max(0f, 1f - cpuP95F),
//                IdleFraction = idleFrac,
//                OffhoursIdleFraction = offIdleFr,
//                DataCompleteness = completeness,
//                AllocMemGiB = allocGiB
//            });
//        }

//        // APM TX → signals
//        var apmMap = new Dictionary<string, ApmSignals>(StringComparer.OrdinalIgnoreCase);
//        if (!string.IsNullOrWhiteSpace(apmDir) && Directory.Exists(apmDir))
//        {
//            logger.LogInformation("APM: Reading transaction files from {Dir}", apmDir);
//            var map = new Dictionary<string, ApmAcc>(StringComparer.OrdinalIgnoreCase);

//            foreach (var file in Directory.EnumerateFiles(apmDir, "*.ndjson", SearchOption.TopDirectoryOnly))
//            {
//                int fileLines = 0;
//                try
//                {
//                    await foreach (var doc in ReadNdjson(file))
//                    {
//                        fileLines++;
//                        string host = "";
//                        if (doc.TryGetProperty("host", out var hostObj) && hostObj.TryGetProperty("name", out var nameProp))
//                            host = nameProp.GetString() ?? "";
//                        if (string.IsNullOrEmpty(host)) continue;

//                        var accApm = map.TryGetValue(host, out var a) ? a : (map[host] = new ApmAcc());
//                        accApm.Total++;

//                        // outcome for error rate
//                        var outcome = TryString(doc, "event.outcome");
//                        if (string.Equals(outcome, "failure", StringComparison.OrdinalIgnoreCase))
//                            accApm.Fail++;

//                        // latency microseconds
//                        var durUs = TryNum(doc, "transaction.duration.us");
//                        if (!double.IsNaN(durUs)) accApm.P95Latency.Add(durUs);

//                        // service name
//                        var svc = TryString(doc, "service.name");
//                        if (!string.IsNullOrEmpty(svc))
//                            accApm.ServiceCounts[svc] = accApm.ServiceCounts.TryGetValue(svc, out var c) ? c + 1 : 1;

//                        // minute key (crude rpm estimate)
//                        if (doc.TryGetProperty("@timestamp", out var tsProp))
//                        {
//                            var ts = tsProp.GetDateTime().ToUniversalTime().ToString("yyyyMMddHHmm");
//                            accApm.PerMinute[ts] = accApm.PerMinute.TryGetValue(ts, out var count) ? count + 1 : 1;
//                        }
//                    }
//                    logger.LogInformation("APM: Parsed {Lines} lines from {File}", fileLines, file);
//                }
//                catch (Exception exFile)
//                {
//                    LogException(logger, $"APM: parsing file '{file}' failed", exFile);
//                }
//            }

//            // materialize signals
//            foreach (var (host, a) in map)
//            {
//                var errRate = a.Total > 0 ? (float)a.Fail / a.Total : 0f;
//                var p95ms = (float)(double.IsNaN(a.P95Latency.Value) ? 0 : a.P95Latency.Value / 1000.0); // us → ms

//                var rpmQ = new P2Quantile(0.95);
//                foreach (var v in a.PerMinute.Values) rpmQ.Add(v);
//                var rpm95 = (float)(double.IsNaN(rpmQ.Value) ? 0 : rpmQ.Value);

//                string? topService = a.ServiceCounts.Count == 0
//                    ? null
//                    : a.ServiceCounts.OrderByDescending(kv => kv.Value).First().Key;

//                apmMap[host] = new ApmSignals
//                {
//                    Host = host,
//                    ErrorRate = errRate,
//                    LatencyP95Ms = p95ms,
//                    ApproxRpmP95 = rpm95,
//                    TopService = topService
//                };
//            }

//            // Inject RPM proxy into HostFeatures.RpmP95 where possible
//            foreach (var hf in outList)
//                if (apmMap.TryGetValue(hf.Host, out var sig))
//                    hf.RpmP95 = Math.Max(hf.RpmP95, sig.ApproxRpmP95);
//        }
//        else
//        {
//            logger.LogWarning("APM: directory not found or not provided: {Dir}", apmDir ?? "(null)");
//        }

//        logger.LogInformation("Features+APM: Built HostFeatures={Count} and ApmSignals={ApmCount}", outList.Count, apmMap.Count);
//        return (outList, apmMap);
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

//    private static string? TryString(JsonElement root, string dottedPath)
//    {
//        if (root.TryGetProperty(dottedPath, out var v) && v.ValueKind == JsonValueKind.String) return v.GetString();

//        var parts = dottedPath.Split('.', 2);
//        if (parts.Length == 2 && root.TryGetProperty(parts[0], out var first) && first.ValueKind == JsonValueKind.Object)
//        {
//            if (first.TryGetProperty(parts[1], out var vv) && vv.ValueKind == JsonValueKind.String) return vv.GetString();
//        }
//        return null;
//    }
//}
