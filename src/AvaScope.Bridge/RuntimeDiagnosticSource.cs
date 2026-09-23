namespace AvaScope.Bridge;

/// <summary>Opt-in host adapter. Publish only approved diagnostics for its registered top-level.</summary>
public sealed class RuntimeDiagnosticSource : IDisposable
{
    private Func<string, string, string?, string?, string?, bool>? _report;
    private Action? _unregister;
    internal RuntimeDiagnosticSource(Func<string, string, string?, string?, string?, bool> report, Action unregister)
    { _report = report; _unregister = unregister; }

    public bool Report(string level, string message, string? requestId = null, string? operationId = null, string? automationId = null)
        => Volatile.Read(ref _report)?.Invoke(level, message, requestId, operationId, automationId) ?? false;

    public void Dispose()
    {
        Interlocked.Exchange(ref _report, null);
        Interlocked.Exchange(ref _unregister, null)?.Invoke();
    }
}
