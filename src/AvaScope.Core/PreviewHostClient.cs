using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AvaScope.Protocol;
using SkiaSharp;

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

    public async Task<CoreResult<PreviewBatchResponse>> RenderBatchAsync(
        PreviewRequest request,
        IReadOnlyList<PreviewViewport> viewports,
        string? contactSheetPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(viewports);

        if (viewports.Count == 0)
        {
            return CoreResult<PreviewBatchResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidPreviewRequest,
                "At least one preview viewport size is required."));
        }

        var entries = new List<PreviewBatchEntry>(viewports.Count);
        for (var index = 0; index < viewports.Count; index++)
        {
            var viewport = viewports[index];
            var outputPath = CreateViewportOutputPath(request.OutputPath, viewport, index);
            var viewportRequest = new PreviewRequest(
                outputPath,
                viewport.Width,
                viewport.Height,
                request.Dpi,
                request.ProjectPath,
                request.ViewPath,
                request.ThemeVariant,
                request.Culture,
                request.DesignDataType);
            var result = await RenderAsync(viewportRequest, cancellationToken);
            entries.Add(new PreviewBatchEntry(
                viewport,
                outputPath,
                result.Success
                    ? ToolResult<PreviewResponse>.Ok(result.Value!)
                    : ToolResult<PreviewResponse>.Fail(new ProtocolError(
                        result.Error!.Code,
                        result.Error.Message,
                        result.Error.Details))));
        }

        var fullContactSheetPath = string.IsNullOrWhiteSpace(contactSheetPath)
            ? null
            : Path.GetFullPath(contactSheetPath);
        if (fullContactSheetPath is not null)
        {
            var sheetResult = TryCreateContactSheet(entries, fullContactSheetPath);
            if (!sheetResult.Success)
            {
                return CoreResult<PreviewBatchResponse>.Fail(sheetResult.Error!);
            }
        }

        return CoreResult<PreviewBatchResponse>.Ok(new PreviewBatchResponse(
            entries,
            fullContactSheetPath,
            DateTimeOffset.UtcNow));
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
                result.Error.Message,
                result.Error.Details));
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

    private static string CreateViewportOutputPath(string baseOutputPath, PreviewViewport viewport, int index)
    {
        var fullBasePath = Path.GetFullPath(baseOutputPath);
        var directory = Path.GetDirectoryName(fullBasePath) ?? Environment.CurrentDirectory;
        var stem = Path.GetFileNameWithoutExtension(fullBasePath);
        var extension = Path.GetExtension(fullBasePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        var label = string.IsNullOrWhiteSpace(viewport.Label)
            ? $"{FormatSizeToken(viewport.Width)}x{FormatSizeToken(viewport.Height)}"
            : SanitizePathToken(viewport.Label);
        var fileName = $"{stem}-{index + 1:00}-{label}{extension}";
        return Path.Combine(directory, fileName);
    }

    private static string FormatSizeToken(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', '_');
    }

    private static string SanitizePathToken(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Trim()
            .Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
            .ToArray();
        var token = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(token) ? "viewport" : token;
    }

    private static CoreResult<bool> TryCreateContactSheet(
        IReadOnlyList<PreviewBatchEntry> entries,
        string contactSheetPath)
    {
        try
        {
            var bitmaps = entries
                .Where(static entry => entry.Render.Success && File.Exists(entry.Render.Value!.FilePath))
                .Select(static entry => SKBitmap.Decode(entry.Render.Value!.FilePath))
                .Where(static bitmap => bitmap is not null)
                .Cast<SKBitmap>()
                .ToArray();
            try
            {
                if (bitmaps.Length == 0)
                {
                    return CoreResult<bool>.Fail(new CoreError(
                        CoreErrorCodes.PreviewHostFailed,
                        "Contact sheet requires at least one successful preview image."));
                }

                var width = bitmaps.Max(static bitmap => bitmap.Width);
                var height = bitmaps.Sum(static bitmap => bitmap.Height);
                using var sheet = new SKBitmap(width, height);
                using var canvas = new SKCanvas(sheet);
                canvas.Clear(SKColors.Transparent);

                var offsetY = 0;
                foreach (var bitmap in bitmaps)
                {
                    canvas.DrawBitmap(bitmap, 0, offsetY);
                    offsetY += bitmap.Height;
                }

                var directory = Path.GetDirectoryName(contactSheetPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var image = SKImage.FromBitmap(sheet);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(contactSheetPath);
                data.SaveTo(stream);
                return CoreResult<bool>.Ok(true);
            }
            finally
            {
                foreach (var bitmap in bitmaps)
                {
                    bitmap.Dispose();
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return CoreResult<bool>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                $"Contact sheet could not be created: {exception.Message}"));
        }
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
