using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using System.Reflection;
using System.Text.Json;
using Volo.Abp;


internal class Program
{
    async static Task Main(string[] args)
    {
        using (var application = AbpApplicationFactory.Create<FinolyzerMCPClientModule>(options =>
        {
            options.UseAutofac(); // Use Autofac DI
        }))
        {
            application.Initialize();

            // Create SK kernel builder and configure services
            var builder = Kernel.CreateBuilder();
            builder.Services.AddLogging(c => c.AddDebug().SetMinimumLevel(LogLevel.Trace));
            builder.AddOpenAIChatCompletion("gpt-4o-mini", "YOUR_API_KEY");

            var kernel = builder.Build();

            // MCP client setup
            var serializerOptions = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            //await using IMcpClient fsMcpClient = await McpClientFactory.CreateAsync(
            //    new StdioClientTransport(new()
            //    {
            //        Command = "dotnet",
            //        Arguments = new[]
            //        {
            //"run",
            //"--project",
            //""""
            //        },
            //        WorkingDirectory = "F:\\Finolyzer\\Finolyzer"
            //    }));



            var clientTransport = new StdioClientTransport(new()
            {
                Name = "Finolyzer",
                Command = "dotnet",
                Arguments = ["run", "--project", "F:\\Finolyzer\\Finolyzer\\Finolyzer\\Finolyzer.csproj"],
            });
            Console.WriteLine("📡 Connecting to MCP server...");

            await using var mcpClient = await McpClientFactory.CreateAsync(clientTransport);
            Console.WriteLine("✅ Connected to MCP server successfully!");
            Console.WriteLine("\n📋 Listing available tools:");
            var tools = await mcpClient.ListToolsAsync();
            foreach (var tool in tools)
            {
                Console.WriteLine($"  - {tool.Name}: {tool.Description}");
            }

            // Test calculator operations
            Console.WriteLine("\n🧮 Testing Calculator Operations:");


            var timlyInput = new Dictionary<string, object?>
            {
                ["description"] = "invoice",
                ["notes"] = "system",
                ["entityId"] = 2,
                ["timlyRequestType"] = "Monthly",
                ["calculationFor"] = "System",
                ["calculationBeforeDate"] = "2025-07-17",
                ["includeSharedService"] = true
            };

            try
            {
                var result = await mcpClient.CallToolAsync(
                    "calculate_system_cost",
                    timlyInput,
                cancellationToken: CancellationToken.None
                );
                Console.WriteLine("✅ Result:\n" + ExtractTextResult(result));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error calling CalculateAsync: {ex.Message}");
            }
        }
    }
    /// <summary>
    /// Extracts the text result from a tool call response object.
    /// </summary>
    /// <param name="result">The result object, which may contain text content or other data.</param>
    /// <returns>
    /// A string containing the extracted text if found, a serialized representation of the result if no text is found, 
    /// or a fallback string if serialization fails.
    /// </returns>
    static string ExtractTextResult(object result)
    {
        try
        {
            if (result is IEnumerable<object> contentList)
            {
                foreach (var content in contentList)
                {
                    if (content is IDictionary<string, object> contentDict &&
                        contentDict.TryGetValue("text", out var text))
                    {
                        return text?.ToString() ?? "No text content";
                    }
                }
            }

            // Fallback: try to serialize the entire result
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return result?.ToString() ?? "No result";
        }
    }
}