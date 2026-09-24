using System.Text.Json;
using ModelContextProtocol.Client;

if (args.Length is < 3 or > 5 || args.Length == 5 && args[4] != "--full-result")
{
    Console.Error.WriteLine("Usage: AvaScope.McpScenarioClient <mcp-assembly> <request-json> <manifest-directory> [tool-name] [--full-result]");
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
if (args.Length >= 4 && args[3] == "--stdio-session") cancellation.CancelAfter(TimeSpan.FromMinutes(10));
var fullResult = args.Length == 5;
var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
// Unix NamedPipeStream resolves its socket beneath TMPDIR. Preserve that transport
// environment when connecting to an app launched outside this sanitized MCP child.
// Preserve explicit evidence/session stores so isolated scenarios retain ownership.
foreach (var name in new[] { "TMPDIR", "DISPLAY", "XAUTHORITY", "WAYLAND_DISPLAY", "XDG_RUNTIME_DIR", "XDG_DATA_DIRS", "LD_LIBRARY_PATH", "DBUS_SESSION_BUS_ADDRESS", "LANG", "LC_ALL", "AVASCOPE_PROFILE_TEST_SECRET", "AVASCOPE_RESPONSE_ARTIFACT_DIR", "AVASCOPE_RUN_STORE_DIR", "AVASCOPE_PREVIEW_SESSION_STORE", "AVASCOPE_RECIPE_HEALTHY", "AVASCOPE_RECIPE_BROKEN" })
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
var toolName = args.Length >= 4 ? args[3] : "run_scenario";
if (toolName == "--schemas")
{
    var schemas = await client.ListToolsAsync(cancellationToken: cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        server = client.ServerInfo,
        tools = schemas.ToDictionary(tool => tool.Name, tool => tool.ProtocolTool.InputSchema)
    }));
    return 0;
}
if (toolName == "--stdio-session")
{
    for (var index = 0; index < 128; index++)
    {
        var line = await Console.In.ReadLineAsync(cancellation.Token);
        if (line is null) break;
        if (line.Length > 1024 * 1024) throw new ArgumentException("A probe command is limited to 1 MiB.");
        using var command = JsonDocument.Parse(line);
        var name = command.RootElement.GetProperty("tool").GetString()!;
        var arguments = command.RootElement.GetProperty("arguments").EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value);
        var measurement = command.RootElement.TryGetProperty("measurement", out var measured) ? measured : default;
        var called = await CallMeasuredAsync(name, arguments, measurement);
        Console.WriteLine(fullResult ? JsonSerializer.Serialize(called) : JsonSerializer.Serialize(called.StructuredContent));
    }
    return 0;
}
var toolArguments = args.Length >= 4
    ? request.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value)
    : new Dictionary<string, object?>
    {
        ["request"] = request,
        ["manifestDirectory"] = manifestDirectory
    };
var result = await CallMeasuredAsync(toolName, toolArguments, default);
var output = fullResult ? JsonSerializer.Serialize(result) : JsonSerializer.Serialize(result.StructuredContent);
Console.WriteLine(output);
return result.IsError != true && result.StructuredContent is { ValueKind: JsonValueKind.Object } structured
    && structured.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True ? 0 : 1;

async Task<ModelContextProtocol.Protocol.CallToolResult> CallMeasuredAsync(string name, Dictionary<string, object?> arguments, JsonElement measurement)
{
    var timer = System.Diagnostics.Stopwatch.StartNew();
    ModelContextProtocol.Protocol.CallToolResult? called = null;
    try
    {
        called = await client.CallToolAsync(name, arguments, cancellationToken: cancellation.Token);
        return called;
    }
    finally
    {
        // Opt-in local measurement for agent-directed evaluations. Never record arguments or text content.
        if (Environment.GetEnvironmentVariable("AVASCOPE_AGENT_TRACE_FILE") is { Length: > 0 } tracePath)
        {
            var trial = String(measurement, "trialId") ?? Environment.GetEnvironmentVariable("AVASCOPE_AGENT_TRIAL_ID") ?? "unreported";
            var context = String(measurement, "contextRevision") ?? Environment.GetEnvironmentVariable("AVASCOPE_AGENT_CONTEXT") ?? "unreported";
            var retry = String(measurement, "retryDisposition") ?? Environment.GetEnvironmentVariable("AVASCOPE_AGENT_RETRY") ?? "unknown";
            var content = called?.StructuredContent;
            var value = content is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("value", out var supplied) ? supplied : default;
            var capturedSuccess = content is { ValueKind: JsonValueKind.Object } response && response.TryGetProperty("success", out var passed) && passed.ValueKind == JsonValueKind.True;
            var metadata = new
            {
                schemaVersion = 1, mode = "agent_tool_capture", trialId = trial, contextRevision = context,
                tool = name, serverVersion = client.ServerInfo.Version, completedAt = DateTimeOffset.UtcNow,
                durationMs = timer.ElapsedMilliseconds, success = capturedSuccess, transportCompleted = called is not null,
                status = String(value, "status"), failureStage = String(value, "failureStage"),
                requestFingerprint = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(arguments))),
                retryDisposition = retry
            };
            var fullPath = Path.GetFullPath(tracePath);
            if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 4 * 1024 * 1024) throw new IOException("Agent trace reached its 4 MiB limit; no tool call should be repeated just to record it.");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.AppendAllTextAsync(fullPath, JsonSerializer.Serialize(metadata) + "\n");
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(fullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}

static string? String(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object
    && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
