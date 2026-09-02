using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

var markerPath = ReadOption(args, "--marker");
var secret = Environment.GetEnvironmentVariable("AVASCOPE_LIFECYCLE_TEST_SECRET");
var echoSecret = string.Equals(
    Environment.GetEnvironmentVariable("AVASCOPE_LIFECYCLE_TEST_ECHO_SECRET"),
    "1",
    StringComparison.Ordinal);
_ = int.TryParse(
    Environment.GetEnvironmentVariable("AVASCOPE_LIFECYCLE_TEST_FIRST_RESPONSE_DELAY_MS"),
    out var firstResponseDelayMs);
if (!string.IsNullOrWhiteSpace(markerPath))
{
    var markerDirectory = Path.GetDirectoryName(Path.GetFullPath(markerPath));
    if (!string.IsNullOrWhiteSpace(markerDirectory))
    {
        Directory.CreateDirectory(markerDirectory);
    }

    File.WriteAllText(markerPath, secret ?? "missing");
}

var sessionId = new SessionId($"lifecycle-{Guid.NewGuid():N}");
var pipeName = $"avl-{Guid.NewGuid():N}"[..20];
var process = Process.GetCurrentProcess();
var manifest = new BridgeSessionManifest(
    sessionId,
    process.Id,
    pipeName,
    DateTimeOffset.UtcNow,
    "AvaScope Lifecycle Test App",
    processName: process.ProcessName);
var manifestPath = BridgeSessionManifest.GetDefaultPath(sessionId);
Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
Console.WriteLine("Lifecycle test bridge ready.");
if (echoSecret && !string.IsNullOrWhiteSpace(secret))
{
    Console.WriteLine($"Lifecycle secret: {secret}");
}
var firstResponse = true;

while (true)
{
    await using var pipe = new NamedPipeServerStream(
        pipeName,
        PipeDirection.InOut,
        maxNumberOfServerInstances: 1,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous);
    await pipe.WaitForConnectionAsync();
    var requestLine = await ReadLineAsync(pipe);
    var request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine);
    if (request is null)
    {
        continue;
    }

    if (firstResponse && firstResponseDelayMs > 0)
    {
        firstResponse = false;
        await Task.Delay(firstResponseDelayMs);
    }

    var response = request.Method switch
    {
        BridgeIpcMethods.Health => BridgeIpcResponse.Ok(
            request.RequestId,
            HealthResponse.Current(SessionCapabilitiesResponse.Current(sessionId, process.Id))),
        BridgeIpcMethods.ListTopLevels => BridgeIpcResponse.Ok(
            request.RequestId,
            new TopLevelSummary[]
            {
                new TopLevelSummary(
                    "topLevel:lifecycle",
                    "window",
                    "Lifecycle",
                    640,
                    480,
                    1,
                    true)
            }),
        BridgeIpcMethods.CloseSession => BridgeIpcResponse.Ok(
            request.RequestId,
            new CloseSessionResponse(
                new SessionSummary(
                    sessionId,
                    SessionKinds.Runtime,
                    SessionStates.Closed,
                    manifest.CreatedAt,
                    manifest.DisplayName),
                process.Id,
                DateTimeOffset.UtcNow)),
        _ => BridgeIpcResponse.Fail(
            request.RequestId,
            new ProtocolError("lifecycle_test_method_unsupported", $"Method '{request.Method}' is not supported."))
    };
    var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response) + Environment.NewLine);
    await pipe.WriteAsync(responseBytes);
    await pipe.FlushAsync();
}

static string? ReadOption(IReadOnlyList<string> args, string name)
{
    for (var index = 0; index < args.Count - 1; index++)
    {
        if (string.Equals(args[index], name, StringComparison.Ordinal))
        {
            return args[index + 1];
        }
    }

    return null;
}

static async Task<string> ReadLineAsync(Stream stream)
{
    var bytes = new List<byte>();
    var buffer = new byte[128];
    while (true)
    {
        var read = await stream.ReadAsync(buffer);
        if (read == 0)
        {
            break;
        }

        for (var index = 0; index < read; index++)
        {
            if (buffer[index] == (byte)'\n')
            {
                return Encoding.UTF8.GetString(bytes.ToArray());
            }

            if (buffer[index] != (byte)'\r')
            {
                bytes.Add(buffer[index]);
            }
        }
    }

    return Encoding.UTF8.GetString(bytes.ToArray());
}
