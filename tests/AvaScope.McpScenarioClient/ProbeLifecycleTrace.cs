using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AvaScope.Testing;

// Shared only by the subprocess probe and its tests. Never capture protocol payloads.
internal sealed class ProbeLifecycleTrace(string? path, Action<string>? output = null, bool expectsOutput = true) : IDisposable
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private FileStream? _stream;
    private int _events;
    private int _requests;
    private int _responses;
    private int _outputs;
    private bool _unavailable;

    public void Record(string stage, Exception? error = null, int? serverProcessId = null, int? serverExitCode = null)
    {
        if (path is null && output is null || _unavailable) return;
        if (stage == "request_started") _requests++;
        if (stage == "response_received") _responses++;
        if (stage == "output_written") _outputs++;
        try
        {
            if (++_events > 1024) throw new IOException();
            var line = JsonSerializer.Serialize(new
            {
                schemaVersion = 1, mode = "probe_lifecycle", stage, sequence = _events,
                observedAt = DateTimeOffset.UtcNow, elapsedMs = _elapsed.Elapsed.TotalMilliseconds,
                processId = Environment.ProcessId, serverProcessId, serverExitCode,
                requestsStarted = _requests, responsesReceived = _responses, outputsWritten = _outputs, expectsOutput,
                disposition = _requests > _responses ? "request_outcome_unknown"
                    : expectsOutput && _responses > _outputs ? "response_received_output_pending"
                    : _requests > 0 ? expectsOutput ? "responses_written" : "responses_received" : "no_tool_request_started",
                failure = error switch
                {
                    OperationCanceledException => "canceled", TimeoutException => "timeout",
                    IOException => "io", JsonException => "json", null => null, _ => "other"
                }
            });
            output?.Invoke(line);
            if (path is null) return;
            if (_stream is null)
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.Read };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                _stream = new FileStream(path, options);
            }
            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            if (_stream.Length + bytes.Length > 256 * 1024) throw new IOException();
            _stream.Write(bytes);
            _stream.Flush();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            _unavailable = true;
            // A failed evidence write must not turn an already completed edit into a retry.
            Console.Error.WriteLine("Probe lifecycle evidence unavailable; do not replay requests to recover diagnostics.");
        }
    }

    public void Dispose()
    {
        try { _stream?.Dispose(); }
        catch (IOException) { Console.Error.WriteLine("Probe lifecycle evidence could not be closed; do not replay requests to recover diagnostics."); }
    }
}
