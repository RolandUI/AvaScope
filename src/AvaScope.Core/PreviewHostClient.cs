using System.Diagnostics;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewHostClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeSpan _operationTimeout;

    public PreviewHostClient(string? hostAssemblyPath = null, TimeSpan? operationTimeout = null)
    {
        HostAssemblyPath = string.IsNullOrWhiteSpace(hostAssemblyPath)
            ? Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll")
            : hostAssemblyPath;
        _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(60);

        if (_operationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(operationTimeout), operationTimeout, "Timeout must be positive.");
        }
    }

    public string HostAssemblyPath { get; }

    public PreviewHostDiagnostic GetDiagnostics()
    {
        var fullHostAssemblyPath = Path.GetFullPath(HostAssemblyPath);
        if (!File.Exists(fullHostAssemblyPath))
        {
            return new PreviewHostDiagnostic(
                DiagnosticStatuses.Unavailable,
                fullHostAssemblyPath,
                DiagnosticProcessModes.IsolatedChildProcess,
                error: new ProtocolError(
                    CoreErrorCodes.PreviewHostUnavailable,
                    $"Preview host assembly '{fullHostAssemblyPath}' was not found."));
        }

        return new PreviewHostDiagnostic(
            DiagnosticStatuses.Available,
            fullHostAssemblyPath,
            DiagnosticProcessModes.IsolatedChildProcess,
            HealthResponse.Current());
    }

    public async Task<CoreResult<PreviewResponse>> RenderAsync(
        PreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(HostAssemblyPath))
        {
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostUnavailable,
                $"Preview host assembly '{HostAssemblyPath}' was not found."));
        }

        var requestDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.PreviewHostClient",
            Guid.NewGuid().ToString("n"));
        var requestPath = Path.Combine(requestDirectory, "request.json");

        try
        {
            Directory.CreateDirectory(requestDirectory);
            await File.WriteAllTextAsync(
                requestPath,
                JsonSerializer.Serialize(request, JsonOptions),
                cancellationToken);

            return await RunPreviewHostAsync(requestPath, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                exception.Message));
        }
        finally
        {
            TryDeleteDirectory(requestDirectory);
        }
    }

    private async Task<CoreResult<PreviewResponse>> RunPreviewHostAsync(
        string requestPath,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = Path.GetDirectoryName(HostAssemblyPath) ?? AppContext.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add(HostAssemblyPath);
        process.StartInfo.ArgumentList.Add("--request");
        process.StartInfo.ArgumentList.Add(requestPath);

        if (!process.Start())
        {
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostUnavailable,
                $"Could not start preview host '{HostAssemblyPath}'."));
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_operationTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            KillPreviewHost(process);
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostUnavailable,
                "Preview host request timed out."));
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                stderr.Trim()));
        }

        ToolResult<PreviewResponse>? result;
        try
        {
            result = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(stdout, JsonOptions);
        }
        catch (JsonException exception)
        {
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                exception.Message));
        }

        if (result is null)
        {
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                "Preview host returned an empty response."));
        }

        if (!result.Success)
        {
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                result.Error!.Code,
                result.Error.Message));
        }

        if (process.ExitCode != 0)
        {
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                $"Preview host exited with code {process.ExitCode}."));
        }

        return result.Value is null
            ? CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                "Preview host success response did not contain a value."))
            : CoreResult<PreviewResponse>.Ok(result.Value);
    }

    private static void KillPreviewHost(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
