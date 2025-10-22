//using Microsoft.ML;
//using Microsoft.ML.Data;
//using Microsoft.ML.Transforms;
//using System.Text;
//using System.Text.Json;


//// Program.cs
//// .NET 9 Minimal API + ML.NET (PCA anomaly + KMeans clustering + trend guard)
//// Elasticsearch via raw HTTP+JSON (no Nest/Elastic typed agg APIs).
////
//// Endpoints:
////   GET  /health
////   POST /train          -> trains and saves models to ./models
////   GET  /suggestions    -> loads models, scores latest data, returns actions
////
//// Env vars:
////   ES_URL                (e.g., https://your-es:9200)
////   ES_API_KEY_BASE64     (base64 of "id:key")
////   METRICS_INDEX         (default: "metricbeat-*,apm-*-metric-*")
////   APM_TX_INDEX          (default: "apm-*-transaction-*")
////
//// NuGet:
////   Microsoft.ML
////   Microsoft.ML.Mkl.Components (optional, faster)

//using System.Net.Http;
//using System.Text;
//using System.Text.Json;
//using Microsoft.ML;
//using Microsoft.ML.Data;
//using Microsoft.ML.Transforms;

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
//    public static (ITransformer anomalyModel, ITransformer clusterModel) Train(MLContext ml, List<HostFeatures> rows, string modelDir)
//    {
//        var data = ml.Data.LoadFromEnumerable(rows);

//        var anomalyPairs = Columns.AnomalyCols
//            .Select(c => new InputOutputColumnPair(c, c))
//            .ToArray();

//        var clusterPairs = Columns.ClusterCols
//            .Select(c => new InputOutputColumnPair(c, c))
//            .ToArray();

//        var anomalyPipeline =
//            ml.Transforms.ReplaceMissingValues(anomalyPairs, MissingValueReplacingEstimator.ReplacementMode.Mean)
//              .Append(ml.Transforms.NormalizeMinMax(anomalyPairs))
//              .Append(ml.Transforms.Concatenate("Features", Columns.AnomalyCols))
//              .Append(ml.AnomalyDetection.Trainers.RandomizedPca(
//                  featureColumnName: "Features",
//                  rank: 5,
//                  ensureZeroMean: true,
//                  seed: 42));

//        var anomalyModel = anomalyPipeline.Fit(data);

//        var clusterPipeline =
//            ml.Transforms.ReplaceMissingValues(clusterPairs, MissingValueReplacingEstimator.ReplacementMode.Mean)
//              .Append(ml.Transforms.NormalizeMinMax(clusterPairs))
//              .Append(ml.Transforms.Concatenate("Features", Columns.ClusterCols))
//              .Append(ml.Clustering.Trainers.KMeans(featureColumnName: "Features", numberOfClusters: 4));

//        var clusterModel = clusterPipeline.Fit(data);

//        Directory.CreateDirectory(modelDir);
//        ml.Model.Save(anomalyModel, data.Schema, Path.Combine(modelDir, "anomaly.zip"));
//        ml.Model.Save(clusterModel, data.Schema, Path.Combine(modelDir, "cluster.zip"));

//        return (anomalyModel, clusterModel);
//    }
//}

//// -------------------- Trend guard (slope) --------------------

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

//// -------------------- API --------------------

//public class Program
//{
//    internal static string EsUrl => Environment.GetEnvironmentVariable("ES_URL") ?? "https://ELK.THIQAH.SA";
//    internal static string EsApiKeyBase64 => Environment.GetEnvironmentVariable("ES_API_KEY_BASE64") ?? "enhTTEJwb0JTRmJRbGFOT0RVdmY6YlZmd1BwamZTQWVVTTQ2T2Nlek94Zw==";
//    internal static string MetricsIndex => Environment.GetEnvironmentVariable("MB_INDEX") ?? ".ds-metricbeat-*,metricbeat-*,metricbeat-psintg-*,metricbeat-rabbitmq-*,metrics-*-elk,*metricbeat*,*metric*";
//    internal static string ApmTxIndex => Environment.GetEnvironmentVariable("APM_TX_INDEX") ?? "apm-*";

//    public static async Task Main(string[] args)
//    {
//        var builder = WebApplication.CreateBuilder(args);
//        var app = builder.Build();

//        app.MapGet("/health", () => Results.Ok(new { ok = true, ts = DateTime.UtcNow }));

//        app.MapPost("/train", async () =>
//        {
//            var ml = new MLContext(seed: 42);
//            var features = await BuildFeaturesFromElasticsearch(MetricsIndex, ApmTxIndex);
//            if (features.Count == 0)
//                return Results.Ok(new { trained = 0, note = "No features. Check index names / mappings." });

//            var (anomaly, cluster) = ModelTrainer.Train(ml, features, "models");
//            return Results.Ok(new { trained = features.Count, when = DateTime.UtcNow });
//        });

//        app.MapGet("/suggestions", async () =>
//        {
//            var ml = new MLContext();
//            var features = await BuildFeaturesFromElasticsearch(MetricsIndex, ApmTxIndex);
//            if (features.Count == 0)
//                return Results.Ok(new { generated_at = DateTime.UtcNow, results = Array.Empty<object>(), note = "No features." });

//            var anomalyPath = Path.Combine("models", "anomaly.zip");
//            var clusterPath = Path.Combine("models", "cluster.zip");
//            if (!File.Exists(anomalyPath) || !File.Exists(clusterPath))
//                return Results.Problem("Models not found. Call /train first.");

//            using var fsA = File.OpenRead(anomalyPath);
//            using var fsC = File.OpenRead(clusterPath);
//            var anomalyModel = ml.Model.Load(fsA, out _);
//            var clusterModel = ml.Model.Load(fsC, out _);

//            var anomalyEngine = ml.Model.CreatePredictionEngine<HostFeatures, AnomalyOutput>(anomalyModel);
//            var clusterEngine = ml.Model.CreatePredictionEngine<HostFeatures, ClusterOutput>(clusterModel);

//            var results = new List<object>();

//            foreach (var h in features)
//            {
//                var ao = anomalyEngine.Predict(h);
//                var co = clusterEngine.Predict(h);

//                var cpuSeries = await GetHourlyCpuSeriesForHost(h.Host, 30, MetricsIndex);
//                var trendingUp = TrendGuard.IsTrendingUp(cpuSeries);

//                var suggs = new List<object>();

//                if ((IsIdleCluster(co.ClusterId) || ao.Score >= 0.7f) && !trendingUp && h.DataCompleteness >= 0.9f)
//                {
//                    var targetGiB = Math.Max(2, (int)Math.Ceiling(h.MemP95GiB * 1.25));
//                    suggs.Add(new
//                    {
//                        type = "rightsize_memory",
//                        message = $"{h.Host}: model flags under-utilization; set RAM ≈ {targetGiB} GiB (p95 {h.MemP95GiB:0.0} GiB).",
//                        confidence = Confidence(h, ao, trendingUp)
//                    });

//                    var targetCpuTier = MapCpuToTier(h.CpuP95);
//                    suggs.Add(new
//                    {
//                        type = "rightsize_cpu",
//                        message = $"{h.Host}: p95 CPU {h.CpuP95 * 100:0.#}% → suggest tier {targetCpuTier}.",
//                        confidence = Confidence(h, ao, trendingUp)
//                    });
//                }

//                if (h.OffhoursIdleFraction >= 0.8f && !trendingUp)
//                {
//                    suggs.Add(new
//                    {
//                        type = "offhours_shutdown",
//                        message = $"{h.Host}: off-hours idle {h.OffhoursIdleFraction:P0} → schedule stop 00:00–07:00.",
//                        confidence = 0.65
//                    });
//                }

//                if (h.RpmP95 < 1 && h.CpuP95 < 0.05f && (h.NetInP95Kbps + h.NetOutP95Kbps) < 10)
//                {
//                    suggs.Add(new
//                    {
//                        type = "shutdown_candidate",
//                        message = $"{h.Host}: near-zero traffic and CPU; candidate to turn off / consolidate.",
//                        confidence = 0.7
//                    });
//                }

//                if (suggs.Count > 0)
//                    results.Add(new { host = h.Host, suggestions = suggs });
//            }

//            return Results.Json(new { generated_at = DateTime.UtcNow, results });
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

//    // -------------------- Elasticsearch (raw HTTP + JSON) --------------------

//    private static HttpClient CreateEsHttp(string esUrl, string apiKeyBase64)
//    {
//        var http = new HttpClient
//        {
//            BaseAddress = new Uri(esUrl.TrimEnd('/') + "/"),
//            Timeout = TimeSpan.FromSeconds(1000)
//        };
//        http.DefaultRequestHeaders.Add("Authorization", $"ApiKey {apiKeyBase64}");
//        return http;
//    }

//    private static async Task<JsonDocument> EsSearchAsync(HttpClient http, string index, string jsonBody)
//    {
//        using var req = new HttpRequestMessage(HttpMethod.Post, $"{index}/_search")
//        {
//            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
//        };
//        using var res = await http.SendAsync(req);
//        res.EnsureSuccessStatusCode();
//        using var stream = await res.Content.ReadAsStreamAsync();
//        return await JsonDocument.ParseAsync(stream);
//    }

//    // --- Build features (30d) from both Metricbeat and APM metrics
//    private static async Task<List<HostFeatures>> BuildFeaturesFromElasticsearch(
//        string metricsIndex = "metricbeat-*,apm-*-metric-*",
//        string apmTxIndex = "apm-*-transaction-*")
//    {
//        using var http = CreateEsHttp(EsUrl, EsApiKeyBase64);

//        // 1) Per-host stats: memory used (script), cpu p95 from either field, network (if present), idle & completeness
//        var mbBody = @"
//{
//  ""size"": 0,
//  ""query"": {
//    ""bool"": {
//      ""filter"": [
//        { ""range"": { ""@timestamp"": { ""gte"": ""now-1h/h"", ""lt"": ""now"" } } },
//        { ""exists"": { ""field"": ""host.name"" } }
//      ]
//    }
//  },
//  ""aggs"": {
//    ""by_host"": {
//      ""terms"": { ""field"": ""host.name"", ""size"": 50 },
//      ""aggs"": {
//        ""mem_used_p"": {
//          ""percentiles"": {
//            ""script"": {
//              ""lang"": ""painless"",
//              ""source"": ""def t = (doc.containsKey('system.memory.total') && !doc['system.memory.total'].empty) ? doc['system.memory.total'].value : null; def f = (doc.containsKey('system.memory.actual.free') && !doc['system.memory.actual.free'].empty) ? doc['system.memory.actual.free'].value : null; if (t == null || f == null) return null; return t - f;""
//            },
//            ""percents"": [50,95,99]
//          }
//        },
//        ""mem_total"":  { ""max"": { ""field"": ""system.memory.total"" } },

//        ""cpu_p"": {
//          ""percentiles"": {
//            ""script"": {
//              ""lang"": ""painless"",
//              ""source"": ""if (!doc['system.cpu.total.norm.pct'].empty) return doc['system.cpu.total.norm.pct'].value; if (!doc['system.process.cpu.total.norm.pct'].empty) return doc['system.process.cpu.total.norm.pct'].value; return null;""
//            },
//            ""percents"": [50,95,99]
//          }
//        },

//        ""net_in_p"":   { ""percentiles"": { ""field"": ""system.network.in.bytes"",  ""percents"": [95] } },
//        ""net_out_p"":  { ""percentiles"": { ""field"": ""system.network.out.bytes"", ""percents"": [95] } },

//        ""total_docs"": { ""value_count"": { ""field"": ""@timestamp"" } },

//        ""idle_docs"": {
//          ""filter"": {
//            ""bool"": {
//              ""should"": [
//                { ""range"": { ""system.cpu.total.norm.pct"": { ""lt"": 0.05 } } },
//                { ""range"": { ""system.process.cpu.total.norm.pct"": { ""lt"": 0.05 } } }
//              ],
//              ""minimum_should_match"": 1
//            }
//          },
//          ""aggs"": { ""cnt"": { ""value_count"": { ""field"": ""@timestamp"" } } }
//        },

//        ""off_idle_docs"": {
//          ""filter"": {
//            ""bool"": {
//              ""must"": [
//                { ""script"": { ""script"": { ""lang"": ""painless"", ""source"": ""doc['@timestamp'].value.getHour() < 7"" } } },
//                {
//                  ""bool"": {
//                    ""should"": [
//                      { ""range"": { ""system.cpu.total.norm.pct"": { ""lt"": 0.05 } } },
//                      { ""range"": { ""system.process.cpu.total.norm.pct"": { ""lt"": 0.05 } } }
//                    ],
//                    ""minimum_should_match"": 1
//                  }
//                }
//              ]
//            }
//          },
//          ""aggs"": { ""cnt"": { ""value_count"": { ""field"": ""@timestamp"" } } }
//        },

//        ""hours"": {
//          ""date_histogram"": {
//            ""field"": ""@timestamp"",
//            ""calendar_interval"": ""1h"",
//            ""min_doc_count"": 0,
//            ""extended_bounds"": { ""min"": ""now-30d/d"", ""max"": ""now"" }
//          }
//        }
//      }
//    }
//  }
//}";
//        using var mbDoc = await EsSearchAsync(http, metricsIndex, mbBody);

//        // 2) APM transactions → RPM p95 by host (if transactions exist)
//        var apmBody = @"
//{
//  ""size"": 0,
//  ""query"": {
//    ""bool"": {
//      ""filter"": [
//        { ""range"": { ""@timestamp"": { ""gte"": ""now-30d/d"", ""lt"": ""now"" } } },
//        { ""term"": { ""processor.event"": ""transaction"" } }
//      ]
//    }
//  },
//  ""aggs"": {
//    ""by_host"": {
//      ""terms"": { ""field"": ""host.name"", ""size"": 10000 },
//      ""aggs"": {
//        ""per_min"": {
//          ""date_histogram"": { ""field"": ""@timestamp"", ""calendar_interval"": ""1m"" },
//          ""aggs"": { ""tps"": { ""value_count"": { ""field"": ""transaction.id"" } } }
//        },
//        ""rpm_p95"": { ""percentiles_bucket"": { ""buckets_path"": ""per_min>tps"", ""percents"": [95] } }
//      }
//    }
//  }
//}";
//        using var apmDoc = await EsSearchAsync(http, apmTxIndex, apmBody);

//        var rpmByHost = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
//        if (apmDoc.RootElement.TryGetProperty("aggregations", out var apmAggs) &&
//            apmAggs.TryGetProperty("by_host", out var apmByHost) &&
//            apmByHost.TryGetProperty("buckets", out var apmBuckets) &&
//            apmBuckets.ValueKind == JsonValueKind.Array)
//        {
//            foreach (var b in apmBuckets.EnumerateArray())
//            {
//                var host = b.GetProperty("key").GetString() ?? "";
//                float rpm95 = 0;
//                if (b.TryGetProperty("rpm_p95", out var rp) &&
//                    rp.TryGetProperty("values", out var vals) &&
//                    vals.TryGetProperty("95.0", out var v95) &&
//                    v95.ValueKind == JsonValueKind.Number)
//                {
//                    rpm95 = (float)v95.GetDouble();
//                }
//                if (!string.IsNullOrEmpty(host))
//                    rpmByHost[host] = rpm95;
//            }
//        }

//        // Parse per-host buckets
//        var outList = new List<HostFeatures>();
//        const int expectedHours = 24 * 30;

//        if (mbDoc.RootElement.TryGetProperty("aggregations", out var aggs) &&
//            aggs.TryGetProperty("by_host", out var byHost) &&
//            byHost.TryGetProperty("buckets", out var buckets) &&
//            buckets.ValueKind == JsonValueKind.Array)
//        {
//            foreach (var b in buckets.EnumerateArray())
//            {
//                var host = b.GetProperty("key").GetString() ?? "";
//                if (string.IsNullOrEmpty(host)) continue;

//                double memTotal = GetAggValue(b, "mem_total");
//                double memP95 = GetPercentile(b, "mem_used_p", "95.0");
//                double cpuP95 = GetPercentile(b, "cpu_p", "95.0");
//                double netIn95 = GetPercentile(b, "net_in_p", "95.0");
//                double netOut95 = GetPercentile(b, "net_out_p", "95.0");

//                double totalDocs = GetAggValue(b, "total_docs");
//                double idleDocs = GetNestedAggValue(b, "idle_docs", "cnt");
//                double offIdle = GetNestedAggValue(b, "off_idle_docs", "cnt");

//                int nonEmptyHours = 0;
//                if (b.TryGetProperty("hours", out var hours) &&
//                    hours.TryGetProperty("buckets", out var hb) &&
//                    hb.ValueKind == JsonValueKind.Array)
//                {
//                    foreach (var h in hb.EnumerateArray())
//                    {
//                        var docCount = h.GetProperty("doc_count").GetInt64();
//                        if (docCount > 0) nonEmptyHours++;
//                    }
//                }
//                var completeness = expectedHours > 0 ? (float)nonEmptyHours / expectedHours : 0f;

//                var memP95GiB = (float)(memP95 / Math.Pow(1024, 3));
//                var allocGiB = (float)(memTotal / Math.Pow(1024, 3));
//                var memHead = Math.Max(0f, allocGiB - memP95GiB);
//                var cpuP95F = (float)cpuP95;
//                var cpuHead = Math.Max(0f, 1f - cpuP95F);
//                var idleFrac = (totalDocs > 0) ? (float)(idleDocs / totalDocs) : 0f;
//                var offIdleFr = (totalDocs > 0) ? (float)(offIdle / totalDocs) : 0f;

//                var netInKbps = (float)(netIn95 * 8 / 1024.0);
//                var netOutKbps = (float)(netOut95 * 8 / 1024.0);

//                rpmByHost.TryGetValue(host, out var rpm95);

//                outList.Add(new HostFeatures
//                {
//                    Host = host,
//                    MemP95GiB = memP95GiB,
//                    CpuP95 = cpuP95F,
//                    NetInP95Kbps = netInKbps,
//                    NetOutP95Kbps = netOutKbps,
//                    RpmP95 = rpm95,
//                    MemHeadroomGiB = memHead,
//                    CpuHeadroom = cpuHead,
//                    IdleFraction = idleFrac,
//                    OffhoursIdleFraction = offIdleFr,
//                    DataCompleteness = Math.Clamp(completeness, 0f, 1f),
//                    AllocMemGiB = allocGiB
//                });
//            }
//        }

//        return outList;

//        // ---- local JSON helpers
//        static double GetAggValue(JsonElement bucket, string name)
//        {
//            if (bucket.TryGetProperty(name, out var agg) &&
//                agg.TryGetProperty("value", out var v) &&
//                v.ValueKind == JsonValueKind.Number)
//                return v.GetDouble();
//            return 0d;
//        }

//        static double GetNestedAggValue(JsonElement bucket, string aggName, string innerName)
//        {
//            if (bucket.TryGetProperty(aggName, out var agg) &&
//                agg.TryGetProperty(innerName, out var inner) &&
//                inner.TryGetProperty("value", out var v) &&
//                v.ValueKind == JsonValueKind.Number)
//                return v.GetDouble();
//            return 0d;
//        }

//        static double GetPercentile(JsonElement bucket, string aggName, string pctKey)
//        {
//            if (bucket.TryGetProperty(aggName, out var agg) &&
//                agg.TryGetProperty("values", out var vals) &&
//                vals.TryGetProperty(pctKey, out var v) &&
//                v.ValueKind == JsonValueKind.Number)
//                return v.GetDouble();
//            return 0d;
//        }
//    }

//    // --- Hourly CPU series (uses whichever CPU field exists)
//    private static async Task<float[]> GetHourlyCpuSeriesForHost(
//        string host,
//        int days = 30,
//        string metricsIndex = "metricbeat-*,apm-*-metric-*")
//    {
//        using var http = CreateEsHttp(EsUrl, EsApiKeyBase64);

//        var body = $@"
//{{
//  ""size"": 0,
//  ""query"": {{
//    ""bool"": {{
//      ""filter"": [
//        {{ ""range"": {{ ""@timestamp"": {{ ""gte"": ""now-{days}d/d"", ""lt"": ""now"" }} }} }},
//        {{ ""term"": {{ ""host.name"": ""{host.Replace("\"", "\\\"")}"" }} }}
//      ]
//    }}
//  }},
//  ""aggs"": {{
//    ""per_hour"": {{
//      ""date_histogram"": {{
//        ""field"": ""@timestamp"",
//        ""calendar_interval"": ""1h"",
//        ""min_doc_count"": 0,
//        ""extended_bounds"": {{ ""min"": ""now-{days}d/d"", ""max"": ""now"" }}
//      }},
//      ""aggs"": {{
//        ""cpu_avg"": {{
//          ""avg"": {{
//            ""script"": {{
//              ""lang"": ""painless"",
//              ""source"": ""if (!doc['system.cpu.total.norm.pct'].empty) return doc['system.cpu.total.norm.pct'].value; if (!doc['system.process.cpu.total.norm.pct'].empty) return doc['system.process.cpu.total.norm.pct'].value; return null;""
//            }}
//          }}
//        }}
//      }}
//    }}
//  }}
//}}";

//        using var doc = await EsSearchAsync(http, metricsIndex, body);
//        var hours = new List<float>();
//        if (doc.RootElement.TryGetProperty("aggregations", out var aggs) &&
//            aggs.TryGetProperty("per_hour", out var ph) &&
//            ph.TryGetProperty("buckets", out var buckets) &&
//            buckets.ValueKind == JsonValueKind.Array)
//        {
//            foreach (var b in buckets.EnumerateArray())
//            {
//                float v = 0f;
//                if (b.TryGetProperty("cpu_avg", out var ca) &&
//                    ca.TryGetProperty("value", out var val) &&
//                    val.ValueKind == JsonValueKind.Number)
//                    v = (float)val.GetDouble();
//                hours.Add(v); // 0..1
//            }
//        }
//        return hours.ToArray();
//    }
//}
