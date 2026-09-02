using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class LaunchOwnershipCleanupTests : IDisposable
{
    private readonly string _manifestDirectory = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"lifecycle-ownership-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_manifestDirectory))
        {
            Directory.Delete(_manifestDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExactOwnedProcessTreeTerminationIsIdempotent()
    {
        Directory.CreateDirectory(_manifestDirectory);
        using var process = StartSleepingProcess();
        var sessionId = new SessionId("owned-lifecycle");
        var pipeName = TestPipeNames.New();
        var processStartedAt = process.StartTime.ToUniversalTime();
        WriteManifest(sessionId, process.Id, process.ProcessName, pipeName);
        WriteOwnership(sessionId, process.Id, process.ProcessName, processStartedAt);
        var server = RespondToCloseAsync(pipeName, sessionId, process.Id);
        var client = new LocalBridgeClient(_manifestDirectory);

        var first = await client.CloseSessionAsync(sessionId, terminateLaunchedProcess: true);
        await server;
        var second = await client.CloseSessionAsync(sessionId, terminateLaunchedProcess: true);

        Assert.True(first.Success, first.Error?.Message);
        Assert.Equal(CloseSessionOutcomes.Terminated, first.Value!.Outcome);
        Assert.True(first.Value.LaunchedProcessOwned);
        Assert.True(first.Value.ProcessTerminated);
        Assert.True(process.HasExited);
        Assert.True(second.Success, second.Error?.Message);
        Assert.Equal(CloseSessionOutcomes.AlreadyExited, second.Value!.Outcome);
        Assert.True(second.Value.LaunchedProcessOwned);
    }

    [Fact]
    public async Task PidReuseIdentityMismatchNeverTerminatesCurrentProcess()
    {
        Directory.CreateDirectory(_manifestDirectory);
        using var current = Process.GetCurrentProcess();
        var sessionId = new SessionId("pid-reuse-lifecycle");
        var pipeName = TestPipeNames.New();
        WriteManifest(sessionId, current.Id, current.ProcessName, pipeName);
        WriteOwnership(
            sessionId,
            current.Id,
            current.ProcessName,
            current.StartTime.ToUniversalTime().AddDays(-1));
        var server = RespondToCloseAsync(pipeName, sessionId, current.Id);
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.CloseSessionAsync(sessionId, terminateLaunchedProcess: true);
        await server;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(CloseSessionOutcomes.NotOwned, result.Value!.Outcome);
        Assert.False(result.Value.LaunchedProcessOwned);
        Assert.False(result.Value.ProcessTerminated);
        Assert.Contains("reused", result.Value.TerminationMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.False(current.HasExited);
    }

    private static Process StartSleepingProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("Start-Sleep -Seconds 30");
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("sleep 30");
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the lifecycle test process.");
    }

    private void WriteManifest(SessionId sessionId, int processId, string processName, string pipeName)
    {
        var manifest = new BridgeSessionManifest(
            sessionId,
            processId,
            pipeName,
            DateTimeOffset.UtcNow,
            "Lifecycle ownership test",
            processName: processName);
        File.WriteAllText(
            Path.Combine(_manifestDirectory, $"{sessionId.Value}.json"),
            JsonSerializer.Serialize(manifest));
    }

    private void WriteOwnership(
        SessionId sessionId,
        int processId,
        string processName,
        DateTimeOffset processStartedAt)
    {
        var directory = Path.Combine(_manifestDirectory, "launch-ownership");
        Directory.CreateDirectory(directory);
        var session = new SessionSummary(
            sessionId,
            SessionKinds.Runtime,
            SessionStates.Active,
            DateTimeOffset.UtcNow,
            "Lifecycle ownership test");
        var record = new
        {
            session,
            processId,
            processName,
            processStartedAt,
            launchedAt = DateTimeOffset.UtcNow
        };
        File.WriteAllText(
            Path.Combine(directory, $"{sessionId.Value}.json"),
            JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private static async Task RespondToCloseAsync(string pipeName, SessionId sessionId, int processId)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        await pipe.WaitForConnectionAsync(cancellation.Token);
        var requestLine = await ReadLineAsync(pipe, cancellation.Token);
        var request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine)!;
        Assert.Equal(BridgeIpcMethods.CloseSession, request.Method);
        var session = new SessionSummary(
            sessionId,
            SessionKinds.Runtime,
            SessionStates.Closed,
            DateTimeOffset.UtcNow,
            "Lifecycle ownership test");
        var response = BridgeIpcResponse.Ok(
            request.RequestId,
            new CloseSessionResponse(session, processId, DateTimeOffset.UtcNow));
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response) + Environment.NewLine);
        await pipe.WriteAsync(bytes, cancellation.Token);
        await pipe.FlushAsync(cancellation.Token);
    }

    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var buffer = new byte[128];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
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
}
