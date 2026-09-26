using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;
using AvaScope.Core;

if (ReadOption(args, "--native-stream-probe-assembly") is { } captureAssembly)
{
    VerifyNativeStreamCapture(captureAssembly);
    return;
}

var markerPath = ReadOption(args, "--marker");
var failMethod = ReadOption(args, "--fail-method");
var responseGate = ReadOption(args, "--response-gate");
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
var process = Process.GetCurrentProcess();
var pipeName = $"avs-{process.Id}-{Guid.NewGuid().ToString("N")[..16]}";
NamedPipeServerStream CreatePipe() => new(
    pipeName,
    PipeDirection.InOut,
    NamedPipeServerStream.MaxAllowedServerInstances,
    PipeTransmissionMode.Byte,
    PipeOptions.Asynchronous);

var pendingPipe = CreatePipe();
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
var control = new SessionControlCoordinator(sessionId);
if (ReadOption(args, "--control-token-file") is { } tokenFile)
{
    var lease = control.Execute(new("acquire", "fixture-owner", TtlMs: 300000));
    File.WriteAllText(tokenFile, lease.Value!.Token);
}

try
{
    while (true)
    {
        await using var pipe = pendingPipe;
        await pipe.WaitForConnectionAsync();
        // Match the production bridge: preserve queued Unix clients when this reply is disposed.
        pendingPipe = CreatePipe();
        try
        {
            var requestLine = await ReadLineAsync(pipe);
            if (string.IsNullOrWhiteSpace(requestLine)) continue;
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

            if (request.Method == BridgeIpcMethods.ListTopLevels && args.Contains("--stall-top-levels", StringComparer.Ordinal))
            {
                Console.WriteLine($"Lifecycle top-level timeout probe entered at {DateTimeOffset.UtcNow:O}.");
                await Task.Delay(TimeSpan.FromSeconds(60));
            }

            var authorization = BridgeIpcMethods.RequiresControl(request) ? control.Enter(request.ControlToken) : null;
            using var permit = authorization?.Value;
            var response = authorization is { Success: false }
                ? BridgeIpcResponse.Fail(request.RequestId, new ProtocolError(authorization.Error!.Code, authorization.Error.Message))
                : request.Method == failMethod
                ? BridgeIpcResponse.Fail(request.RequestId, new ProtocolError("fixture_failure", $"Requested fixture failure: {failMethod}"))
                : request.Method switch
            {
                BridgeIpcMethods.SessionControl => Control(request),
                BridgeIpcMethods.Health => BridgeIpcResponse.Ok(
                    request.RequestId,
                    HealthResponse.Current(SessionCapabilitiesResponse.Current(sessionId, process.Id))),
                BridgeIpcMethods.ListTopLevels => BridgeIpcResponse.Ok(
                    request.RequestId,
                    args.Contains("--empty-windows", StringComparer.Ordinal) ? [] : new TopLevelSummary[]
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
            if (responseGate is not null)
            {
                File.WriteAllText(responseGate + ".ready", "ready");
                var gateTimer = Stopwatch.StartNew();
                while (!File.Exists(responseGate + ".continue") && gateTimer.Elapsed < TimeSpan.FromSeconds(15))
                    await Task.Delay(10);
                if (!File.Exists(responseGate + ".continue"))
                    throw new TimeoutException("Lifecycle response gate was not released within 15 seconds.");
                responseGate = null;
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // Readiness probes may disconnect; a cancelled observer must not kill the fixture app.
            Console.Error.WriteLine("Lifecycle test client disconnected or sent an incomplete request.");
        }
    }
}
finally
{
    await pendingPipe.DisposeAsync();
}

BridgeIpcResponse Control(BridgeIpcRequest request)
{
    var result = control.Execute(request.SessionControl ?? new());
    return result.Success ? BridgeIpcResponse.Ok(request.RequestId, result.Value)
        : BridgeIpcResponse.Fail(request.RequestId, new ProtocolError(result.Error!.Code, result.Error.Message));
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

static void VerifyNativeStreamCapture(string assemblyPath)
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
    Console.Error.WriteLine("native_stream_probe resolve_capture");
    assemblyPath = Path.GetFullPath(assemblyPath);
    System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) =>
    {
        var path = Path.Combine(Path.GetDirectoryName(assemblyPath)!, name.Name + ".dll");
        return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    };
    var assembly = System.Reflection.Assembly.LoadFrom(assemblyPath);
    var method = assembly.GetType("AvaScope.Tests.Bridge.NativeInputIntegrationTestsAccessibility", throwOnError: true)!
        .GetMethod("ReadCapturedStream", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    var capture = method.CreateDelegate<Func<StreamReader, Task<string>>>();
    Console.Error.WriteLine("native_stream_probe capture_resolved");
    ThreadPool.GetMaxThreads(out _, out var ioMax); ThreadPool.GetMinThreads(out _, out var ioMin);
    if (!ThreadPool.SetMinThreads(1, ioMin) || !ThreadPool.SetMaxThreads(4, ioMax))
        throw new InvalidOperationException("The isolated fixture cannot constrain its worker pool.");

    // Connect before occupying workers; the real writer remains open until explicitly closed.
    var pipeName = "AvaScope.StreamProbe." + Guid.NewGuid().ToString("N");
    using var writer = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.None);
    using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.None);
    var connected = writer.WaitForConnectionAsync(); pipe.Connect(5000);
    connected.GetAwaiter().GetResult();
    Console.Error.WriteLine("native_stream_probe pipe_connected");
    using var heldReader = new StreamReader(pipe, Encoding.UTF8);
    using var release = new ManualResetEventSlim(); using var entered = new CountdownEvent(4); using var finished = new CountdownEvent(4);
    for (var i = 0; i < 4; i++)
        ThreadPool.QueueUserWorkItem(_ => { entered.Signal(); release.Wait(TimeSpan.FromSeconds(30)); finished.Signal(); });

    Process? child = null; Task<string>? stdout = null, stderr = null, held = null;
    var closedComplete = false; var heldBefore = false; var heldAfter = false; var contentVerified = false;
    var workersFinished = false; var childId = 0; var exited = false; var available = -1; long pending = -1; double elapsed = 0;
    try
    {
        if (!entered.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Controlled workers did not start.");
        Console.Error.WriteLine("native_stream_probe workers_occupied");
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("--version"); child = Process.Start(info)!; childId = child.Id;
        stdout = capture(child.StandardOutput); stderr = capture(child.StandardError); held = capture(heldReader);
        Console.Error.WriteLine("native_stream_probe capture_started");
        if (!child.WaitForExit(10000)) throw new TimeoutException("Owned child did not exit.");
        Console.Error.WriteLine("native_stream_probe child_exited");
        exited = child.HasExited;
        var timer = Stopwatch.StartNew(); closedComplete = Task.WhenAll(stdout, stderr).Wait(TimeSpan.FromSeconds(3));
        elapsed = timer.Elapsed.TotalMilliseconds;
        ThreadPool.GetAvailableThreads(out available, out _); pending = ThreadPool.PendingWorkItemCount;
        heldBefore = held.Wait(TimeSpan.FromSeconds(3));
        writer.Dispose(); heldAfter = held.Wait(TimeSpan.FromSeconds(3));
    }
    finally
    {
        release.Set(); writer.Dispose();
        if (child is not null)
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
            child.WaitForExit(5000);
            try
            {
                if (stdout is not null && stderr is not null && held is not null)
                {
                    if (!Task.WhenAll(stdout, stderr, held).Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Capture tasks did not finish after releasing workers and closing the writer.");
                    contentVerified = child.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout.Result)
                        && stderr.Result.Length == 0 && held.Result.Length == 0;
                }
            }
            finally { child.StandardOutput.Dispose(); child.StandardError.Dispose(); child.Dispose(); }
        }
        workersFinished = finished.Wait(TimeSpan.FromSeconds(5));
    }
    Console.WriteLine(JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), childId, childExited = exited,
        captureAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assemblyPath))),
        streamTimeoutMs = 3000, closedStreamsCompleted = closedComplete, closedStreamsElapsedMs = elapsed,
        heldStreamCompletedBeforeEof = heldBefore, heldStreamCompletedAfterEof = heldAfter,
        availableWorkers = available, pendingWorkItems = pending, contentVerified, workersFinished }));
    if (!closedComplete || heldBefore || !heldAfter || !contentVerified || !workersFinished) Environment.ExitCode = 1;
}
