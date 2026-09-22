using System.Text.Json;
using ModelContextProtocol.Client;

if (args.Length is not 3 and not 4)
{
    Console.Error.WriteLine("Usage: AvaScope.McpScenarioClient <mcp-assembly> <request-json> <manifest-directory> [tool-name]");
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
// Unix NamedPipeStream resolves its socket beneath TMPDIR. Preserve that transport
// environment when connecting to an app launched outside this sanitized MCP child.
foreach (var name in new[] { "TMPDIR", "DISPLAY", "XAUTHORITY", "WAYLAND_DISPLAY", "XDG_RUNTIME_DIR", "XDG_DATA_DIRS", "LD_LIBRARY_PATH", "DBUS_SESSION_BUS_ADDRESS", "LANG", "LC_ALL", "AVASCOPE_PROFILE_TEST_SECRET" })
{
    if (Environment.GetEnvironmentVariable(name) is { } value) environment[name] = value;
}
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
var toolName = args.Length == 4 ? args[3] : "run_scenario";
var toolArguments = args.Length == 4
    ? request.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value)
    : new Dictionary<string, object?>
    {
        ["request"] = request,
        ["manifestDirectory"] = manifestDirectory
    };
var result = await client.CallToolAsync(
    toolName,
    toolArguments,
    cancellationToken: cancellation.Token);
var output = JsonSerializer.Serialize(result.StructuredContent);
Console.WriteLine(output);
using var document = JsonDocument.Parse(output);
return document.RootElement.TryGetProperty("success", out var success) && success.GetBoolean()
    ? 0
    : 1;
