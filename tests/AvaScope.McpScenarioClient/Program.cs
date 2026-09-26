using System.Text;
using System.Text.Json;
using AvaScope.Testing;
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

using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
if (args.Length >= 4 && args[3] == "--stdio-session") cancellation.CancelAfter(TimeSpan.FromMinutes(10));
using var lifecycle = new ProbeLifecycleTrace(Environment.GetEnvironmentVariable("AVASCOPE_PROBE_LIFECYCLE_FILE"));
McpClient? client = null;
Exception? primaryFailure = null;
lifecycle.Record("started");
try
{
    lifecycle.Record("request_file_read");
    var request = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(requestPath));
    var fullResult = args.Length == 5;
    var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
    // Unix NamedPipeStream resolves its socket beneath TMPDIR. Preserve that transport
    // environment when connecting to an app launched outside this sanitized MCP child.
    // Preserve explicit evidence/session stores so isolated scenarios retain ownership.
    foreach (var name in new[] { "TMPDIR", "DISPLAY", "XAUTHORITY", "WAYLAND_DISPLAY", "XDG_RUNTIME_DIR", "XDG_DATA_DIRS", "LD_LIBRARY_PATH", "DBUS_SESSION_BUS_ADDRESS", "LANG", "LC_ALL", "AVASCOPE_PROFILE_TEST_SECRET", "AVASCOPE_RESPONSE_ARTIFACT_DIR", "AVASCOPE_RUN_STORE_DIR", "AVASCOPE_PREVIEW_SESSION_STORE", "AVASCOPE_RECIPE_HEALTHY", "AVASCOPE_RECIPE_BROKEN" })
    {
        if (Environment.GetEnvironmentVariable(name) is { } value) environment[name] = value;
    }
    lifecycle.Record("connecting");
    await HoldAsync("connecting");
    client = await McpClient.CreateAsync(
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
    lifecycle.Record("ready");
    var toolName = args.Length >= 4 ? args[3] : "run_scenario";
    if (toolName == "--schemas")
    {
        lifecycle.Record("request_started");
        var schemas = await client.ListToolsAsync(cancellationToken: cancellation.Token);
        lifecycle.Record("response_received");
        lifecycle.Record("output_writing");
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            server = client.ServerInfo,
            tools = schemas.ToDictionary(tool => tool.Name, tool => tool.ProtocolTool.InputSchema)
        }));
        lifecycle.Record("output_written");
        return 0;
    }
    if (toolName == "--stdio-session")
    {
        // This pipe is a UTF-8 JSON protocol, independent of the Windows console code page.
        using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        for (var index = 0; index < 128; index++)
        {
            JsonDocument command;
            try
            {
                lifecycle.Record("command_read");
                var line = await input.ReadLineAsync(cancellation.Token);
                if (line is null) { lifecycle.Record("input_eof"); break; }
                if (line.Length > 1024 * 1024)
                {
                    lifecycle.Record("command_rejected");
                    Console.Error.WriteLine("A probe command is limited to 1048576 characters.");
                    return 2;
                }
                command = JsonDocument.Parse(line);
            }
            catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
            {
                lifecycle.Record("command_rejected", exception);
                Console.Error.WriteLine("Invalid probe command: expected valid UTF-8 and complete JSON.");
                return 2;
            }
            using (command)
            {
                var name = command.RootElement.GetProperty("tool").GetString()!;
                var arguments = command.RootElement.GetProperty("arguments").EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value);
                var measurement = command.RootElement.TryGetProperty("measurement", out var measured) ? measured : default;
                var called = await CallMeasuredAsync(name, arguments, measurement);
                lifecycle.Record("output_writing");
                Console.WriteLine(fullResult ? JsonSerializer.Serialize(called) : JsonSerializer.Serialize(called.StructuredContent));
                lifecycle.Record("output_written");
            }
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
    lifecycle.Record("output_writing");
    Console.WriteLine(output);
    lifecycle.Record("output_written");
    return result.IsError != true && result.StructuredContent is { ValueKind: JsonValueKind.Object } structured
        && structured.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True ? 0 : 1;
}
catch (Exception exception)
{
    primaryFailure = exception;
    lifecycle.Record("failed", exception);
    throw;
}
finally
{
    if (client is not null)
    {
        lifecycle.Record("closing");
        try
        {
            await HoldAsync("closing");
            await client.DisposeAsync();
            var completion = client.Completion.IsCompletedSuccessfully ? client.Completion.Result as StdioClientCompletionDetails : null;
            lifecycle.Record("closed", serverProcessId: completion?.ProcessId, serverExitCode: completion?.ExitCode);
        }
        catch (Exception exception)
        {
            lifecycle.Record("close_failed", exception);
            if (primaryFailure is not null) throw new AggregateException(primaryFailure, exception);
            throw;
        }
    }
}

async Task<ModelContextProtocol.Protocol.CallToolResult> CallMeasuredAsync(string name, Dictionary<string, object?> arguments, JsonElement measurement)
{
    var timer = System.Diagnostics.Stopwatch.StartNew();
    ModelContextProtocol.Protocol.CallToolResult? called = null;
    try
    {
        lifecycle.Record("request_started");
        var pending = client!.CallToolAsync(name, arguments, cancellationToken: cancellation.Token);
        await HoldAsync("request_started");
        called = await pending;
        lifecycle.Record("response_received");
        await HoldAsync("response_received");
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
                tool = name, serverVersion = client!.ServerInfo.Version, completedAt = DateTimeOffset.UtcNow,
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

async Task HoldAsync(string stage)
{
    // An opt-in, finite probe-only delay lets tests inspect real in-flight boundaries.
    // It is not forwarded to the MCP server and never retries a request.
    if (Environment.GetEnvironmentVariable("AVASCOPE_PROBE_LIFECYCLE_FILE") is null
        || Environment.GetEnvironmentVariable("AVASCOPE_PROBE_HOLD_STAGE") != stage
        || Environment.GetEnvironmentVariable("AVASCOPE_PROBE_HOLD_RELEASE") is not { Length: > 0 } release) return;
    lifecycle.Record("controlled_delay_started");
    var timer = System.Diagnostics.Stopwatch.StartNew();
    while (!File.Exists(release) && timer.Elapsed < TimeSpan.FromSeconds(5))
        await Task.Delay(20);
    lifecycle.Record("controlled_delay_finished");
}

static string? String(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object
    && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
