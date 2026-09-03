using System.Text.Json;
using ModelContextProtocol.Client;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: AvaScope.McpScenarioClient <mcp-assembly> <request-json> <manifest-directory>");
    return 2;
}

var serverAssembly = Path.GetFullPath(args[0]);
var requestPath = Path.GetFullPath(args[1]);
var manifestDirectory = Path.GetFullPath(args[2]);
if (!File.Exists(serverAssembly) || !File.Exists(requestPath))
{
    Console.Error.WriteLine("The MCP assembly and scenario request must both exist.");
    return 2;
}

var request = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(requestPath));
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
await using var client = await McpClient.CreateAsync(
    new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "AvaScope complex workflow validation",
        Command = "dotnet",
        Arguments = [serverAssembly],
        WorkingDirectory = Path.GetDirectoryName(serverAssembly),
        InheritEnvironmentVariables = false,
        EnvironmentVariables = environment,
        ShutdownTimeout = TimeSpan.FromSeconds(5)
    }),
    cancellationToken: cancellation.Token);
var result = await client.CallToolAsync(
    "run_scenario",
    new Dictionary<string, object?>
    {
        ["request"] = request,
        ["manifestDirectory"] = manifestDirectory
    },
    cancellationToken: cancellation.Token);
var output = JsonSerializer.Serialize(result.StructuredContent);
Console.WriteLine(output);
using var document = JsonDocument.Parse(output);
return document.RootElement.TryGetProperty("success", out var success) && success.GetBoolean()
    ? 0
    : 1;
