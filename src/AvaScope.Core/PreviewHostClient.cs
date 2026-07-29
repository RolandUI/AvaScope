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
                    $"Preview host assembly '{fullHostAssemblyPath}' was not found.",
                    CreateHostReadinessDetails(fullHostAssemblyPath, "host_assembly")));
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
            var fullHostAssemblyPath = Path.GetFullPath(HostAssemblyPath);
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostUnavailable,
                $"Preview host assembly '{fullHostAssemblyPath}' was not found.",
                CreateHostReadinessDetails(fullHostAssemblyPath, "host_assembly")));
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

            var result = await RunPreviewHostAsync(requestPath, cancellationToken);
            return result.Success
                ? CoreResult<PreviewResponse>.Ok(await BoundDiagnosticsAsync(
                    result.Value!,
                    request.DiagnosticOptions,
                    cancellationToken))
                : result;
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

    private static async Task<PreviewResponse> BoundDiagnosticsAsync(
        PreviewResponse response,
        PreviewDiagnosticOptions? options,
        CancellationToken cancellationToken)
    {
        var artifactPath = $"{Path.GetFullPath(response.FilePath)}.diagnostics.json";
        var processed = await new PreviewDiagnosticProcessor().ProcessAsync(
            response.Diagnostics,
            artifactPath,
            options,
            cancellationToken);

        return new PreviewResponse(
            response.FilePath,
            response.PixelWidth,
            response.PixelHeight,
            response.Dpi,
            response.RenderedAt,
            response.ProjectPath,
            response.ViewPath,
            response.ThemeVariant,
            response.Culture,
            response.DesignDataType,
            processed.Diagnostics,
            response.AnimationTimeOffsetMs,
            response.ProjectInfo,
            response.StateVariant,
            response.RunIndex,
            processed.Summary,
            processed.ArtifactPath);
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
                request.DesignDataType,
                stateVariant: request.StateVariant,
                buildOutputRoot: request.BuildOutputRoot,
                assemblyPath: request.AssemblyPath,
                noBuild: request.NoBuild,
                diagnosticOptions: request.DiagnosticOptions);
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
            if (!entries.Any(static entry => entry.Render.Success))
            {
                return CoreResult<PreviewBatchResponse>.Fail(CreateAllPreviewVariantsFailedError(entries, fullContactSheetPath));
            }

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

    private static CoreError CreateAllPreviewVariantsFailedError(
        IReadOnlyList<PreviewBatchEntry> entries,
        string contactSheetPath)
    {
        var firstFailure = entries
            .Select(static entry => entry.Render.Error)
            .FirstOrDefault(static error => error is not null);
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phase"] = "contact_sheet",
            ["contactSheetPath"] = Path.GetFullPath(contactSheetPath),
            ["failedViewports"] = CreateFailedViewportSummary(entries),
            ["nextAction"] = "Inspect firstRootCauseMessage and per-entry render errors; full build output is available in buildLogPath when reported."
        };

        if (firstFailure is not null)
        {
            details["firstRootCauseCode"] = firstFailure.Code;
            details["firstRootCauseMessage"] = firstFailure.Message;
            if (firstFailure.Details is not null)
            {
                foreach (var item in firstFailure.Details.Take(20))
                {
                    details[$"firstRootCause.{item.Key}"] = item.Value;
                }

                if (firstFailure.Details.TryGetValue("buildLogPath", out var buildLogPath))
                {
                    details["buildLogPath"] = buildLogPath;
                }
            }
        }

        return new CoreError(
            firstFailure?.Code ?? CoreErrorCodes.PreviewHostFailed,
            firstFailure is null
                ? "Every preview viewport failed before the contact sheet could be created."
                : $"Every preview viewport failed before the contact sheet could be created. First root cause: {firstFailure.Message}",
            details);
    }

    private static string CreateFailedViewportSummary(IReadOnlyList<PreviewBatchEntry> entries)
    {
        return string.Join(
            " | ",
            entries.Take(12).Select(static entry =>
            {
                var label = string.IsNullOrWhiteSpace(entry.Viewport.Label)
                    ? $"{entry.Viewport.Width.ToString("0.###", CultureInfo.InvariantCulture)}x{entry.Viewport.Height.ToString("0.###", CultureInfo.InvariantCulture)}"
                    : entry.Viewport.Label;
                var error = entry.Render.Error;
                return error is null
                    ? $"{label}:unknown"
                    : $"{label}:{error.Code}:{error.Message}";
            }));
    }

    public async Task<CoreResult<PreviewAnimationResponse>> RenderAnimationAsync(
        PreviewAnimationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var frames = new List<PreviewAnimationFrame>(request.TimeOffsetsMs.Count);
        var cachedFrames = new Dictionary<int, PreviewAnimationFrame>();
        foreach (var offset in request.TimeOffsetsMs)
        {
            var outputPath = CreateAnimationFrameOutputPath(request.OutputPath, offset, frames.Count);
            if (cachedFrames.TryGetValue(offset, out var cachedFrame))
            {
                var copiedFrame = TryCreateCachedAnimationFrame(cachedFrame, outputPath);
                if (!copiedFrame.Success)
                {
                    return CoreResult<PreviewAnimationResponse>.Fail(copiedFrame.Error!);
                }

                frames.Add(copiedFrame.Value!);
                continue;
            }

            var frameRequest = new PreviewRequest(
                outputPath,
                request.Width,
                request.Height,
                request.Dpi,
                request.ProjectPath,
                request.ViewPath,
                request.ThemeVariant,
                request.Culture,
                request.DesignDataType,
                offset,
                request.StateVariant,
                request.BuildOutputRoot,
                request.AssemblyPath,
                request.NoBuild,
                request.DiagnosticOptions);

            var result = await RenderAsync(frameRequest, cancellationToken);
            frames.Add(new PreviewAnimationFrame(
                offset,
                outputPath,
                result.Success
                    ? ToolResult<PreviewResponse>.Ok(result.Value!)
                    : ToolResult<PreviewResponse>.Fail(new ProtocolError(
                        result.Error!.Code,
                        result.Error.Message,
                        result.Error.Details))));
            if (result.Success)
            {
                cachedFrames[offset] = frames[^1];
            }
        }

        var diagnostics = new List<PreviewDiagnostic>();
        var motion = AnalyzeAnimationMotion(frames, diagnostics);
        var fullFrameStripPath = string.IsNullOrWhiteSpace(request.FrameStripPath)
            ? null
            : Path.GetFullPath(request.FrameStripPath);
        if (fullFrameStripPath is not null)
        {
            var stripResult = TryCreateFrameStrip(frames, fullFrameStripPath);
            if (!stripResult.Success)
            {
                return CoreResult<PreviewAnimationResponse>.Fail(stripResult.Error!);
            }
        }

        var sampledAt = DateTimeOffset.UtcNow;
        var processedDiagnostics = await new PreviewDiagnosticProcessor().ProcessAsync(
            diagnostics,
            $"{Path.GetFullPath(request.OutputPath)}.animation.diagnostics.json",
            request.DiagnosticOptions,
            cancellationToken);
        var response = new PreviewAnimationResponse(
            frames,
            fullFrameStripPath,
            motion,
            processedDiagnostics.Diagnostics,
            sampledAt,
            diagnosticSummary: processedDiagnostics.Summary,
            diagnosticsArtifactPath: processedDiagnostics.ArtifactPath);
        if (!string.IsNullOrWhiteSpace(request.ViewerPath))
        {
            var viewerResult = new PreviewAnimationViewerExporter().Export(response, request.ViewerPath);
            if (!viewerResult.Success)
            {
                return CoreResult<PreviewAnimationResponse>.Fail(viewerResult.Error!);
            }

            response = new PreviewAnimationResponse(
                frames,
                fullFrameStripPath,
                motion,
                processedDiagnostics.Diagnostics,
                sampledAt,
                viewerResult.Value,
                processedDiagnostics.Summary,
                processedDiagnostics.ArtifactPath);
        }

        return CoreResult<PreviewAnimationResponse>.Ok(response);
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

        try
        {
            if (!process.Start())
            {
                return CoreResult<PreviewResponse>.Fail(new CoreError(
                    CoreErrorCodes.PreviewHostUnavailable,
                    $"Could not start preview host '{HostAssemblyPath}'.",
                    CreateHostReadinessDetails(HostAssemblyPath, "dotnet_cli")));
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostUnavailable,
                $"Could not start preview host '{HostAssemblyPath}': {exception.Message}",
                CreateHostReadinessDetails(HostAssemblyPath, "dotnet_cli", exception)));
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
                "Preview host request timed out.",
                CreateHostReadinessDetails(HostAssemblyPath, "host_timeout")));
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                stderr.Trim(),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["phase"] = "host",
                    ["hostAssemblyPath"] = Path.GetFullPath(HostAssemblyPath),
                    ["outputTail"] = TrimOutput(stderr),
                    ["nextAction"] = "Inspect preview host stderr and retry after the host can start cleanly."
                }));
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
                exception.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["phase"] = "host",
                    ["hostAssemblyPath"] = Path.GetFullPath(HostAssemblyPath),
                    ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name,
                    ["outputTail"] = TrimOutput(stdout),
                    ["nextAction"] = "Inspect preview host stdout and retry after it returns structured JSON."
                }));
        }

        if (result is null)
        {
            return CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                "Preview host returned an empty response.",
                CreateHostReadinessDetails(HostAssemblyPath, "host_response")));
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
                $"Preview host exited with code {process.ExitCode}.",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["phase"] = "host",
                    ["hostAssemblyPath"] = Path.GetFullPath(HostAssemblyPath),
                    ["exitCode"] = process.ExitCode.ToString(CultureInfo.InvariantCulture),
                    ["nextAction"] = "Inspect the preview host error details and retry after the host process exits successfully."
                }));
        }

        return result.Value is null
            ? CoreResult<PreviewResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                "Preview host success response did not contain a value.",
                CreateHostReadinessDetails(HostAssemblyPath, "host_response")))
            : CoreResult<PreviewResponse>.Ok(result.Value);
    }

    private static IReadOnlyDictionary<string, string> CreateHostReadinessDetails(
        string hostAssemblyPath,
        string requirement,
        Exception? exception = null)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phase"] = "host",
            ["requirement"] = requirement,
            ["hostAssemblyPath"] = Path.GetFullPath(hostAssemblyPath),
            ["nextAction"] = requirement switch
            {
                "host_assembly" => "Build or package AvaScope so AvaScope.PreviewHost.dll is co-located with the caller.",
                "dotnet_cli" => "Install a compatible .NET SDK/runtime and ensure the dotnet executable is on PATH.",
                "host_timeout" => "Check whether the preview host or user project is hanging, then retry with a smaller preview request.",
                _ => "Inspect the preview host process readiness before retrying."
            }
        };

        if (exception is not null)
        {
            details["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name;
        }

        return details;
    }

    private static string TrimOutput(string output)
    {
        var normalized = output.Trim();
        const int maximumLength = 4000;
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[^maximumLength..];
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

    private static string CreateAnimationFrameOutputPath(string baseOutputPath, int timeOffsetMs, int index)
    {
        var fullBasePath = Path.GetFullPath(baseOutputPath);
        var directory = Path.GetDirectoryName(fullBasePath) ?? Environment.CurrentDirectory;
        var stem = Path.GetFileNameWithoutExtension(fullBasePath);
        var extension = Path.GetExtension(fullBasePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        return Path.Combine(directory, $"{stem}-{index + 1:00}-{timeOffsetMs}ms{extension}");
    }

    private static CoreResult<PreviewAnimationFrame> TryCreateCachedAnimationFrame(
        PreviewAnimationFrame cachedFrame,
        string outputPath)
    {
        if (!cachedFrame.Render.Success || cachedFrame.Render.Value is null)
        {
            return CoreResult<PreviewAnimationFrame>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                "Cached animation frame is not successful."));
        }

        var cachedRender = cachedFrame.Render.Value;
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(cachedRender.FilePath, outputPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return CoreResult<PreviewAnimationFrame>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                $"Cached animation frame could not be copied: {exception.Message}"));
        }

        var diagnostics = cachedRender.Diagnostics
            .Append(new PreviewDiagnostic(
                PreviewDiagnosticSeverities.Info,
                PreviewDiagnosticCategories.Animation,
                "animation_frame_reused",
                "PreviewHost reused a previously rendered frame for a repeated animation time offset.",
                details: new Dictionary<string, string>
                {
                    ["timeOffsetMs"] = cachedFrame.TimeOffsetMs.ToString(CultureInfo.InvariantCulture),
                    ["sourceFramePath"] = Path.GetFullPath(cachedRender.FilePath)
                }))
            .ToArray();
        var response = new PreviewResponse(
            outputPath,
            cachedRender.PixelWidth,
            cachedRender.PixelHeight,
            cachedRender.Dpi,
            cachedRender.RenderedAt,
            cachedRender.ProjectPath,
            cachedRender.ViewPath,
            cachedRender.ThemeVariant,
            cachedRender.Culture,
            cachedRender.DesignDataType,
            diagnostics,
            cachedRender.AnimationTimeOffsetMs,
            stateVariant: cachedRender.StateVariant);

        return CoreResult<PreviewAnimationFrame>.Ok(new PreviewAnimationFrame(
            cachedFrame.TimeOffsetMs,
            outputPath,
            ToolResult<PreviewResponse>.Ok(response)));
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

    private static CoreResult<bool> TryCreateFrameStrip(
        IReadOnlyList<PreviewAnimationFrame> frames,
        string frameStripPath)
    {
        try
        {
            var bitmaps = frames
                .Where(static frame => frame.Render.Success && File.Exists(frame.Render.Value!.FilePath))
                .Select(static frame => SKBitmap.Decode(frame.Render.Value!.FilePath))
                .Where(static bitmap => bitmap is not null)
                .Cast<SKBitmap>()
                .ToArray();
            try
            {
                if (bitmaps.Length == 0)
                {
                    return CoreResult<bool>.Fail(new CoreError(
                        CoreErrorCodes.PreviewHostFailed,
                        "Animation frame strip requires at least one successful frame image."));
                }

                var width = bitmaps.Sum(static bitmap => bitmap.Width);
                var height = bitmaps.Max(static bitmap => bitmap.Height);
                using var strip = new SKBitmap(width, height);
                using var canvas = new SKCanvas(strip);
                canvas.Clear(SKColors.Transparent);

                var offsetX = 0;
                foreach (var bitmap in bitmaps)
                {
                    canvas.DrawBitmap(bitmap, offsetX, 0);
                    offsetX += bitmap.Width;
                }

                var directory = Path.GetDirectoryName(frameStripPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var image = SKImage.FromBitmap(strip);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(frameStripPath);
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
                $"Animation frame strip could not be created: {exception.Message}"));
        }
    }

    private static PreviewAnimationMotionSummary AnalyzeAnimationMotion(
        IReadOnlyList<PreviewAnimationFrame> frames,
        List<PreviewDiagnostic> diagnostics)
    {
        var successfulFrames = frames
            .Where(static frame => frame.Render.Success
                && frame.Render.Value is not null
                && File.Exists(frame.Render.Value.FilePath))
            .ToArray();

        if (successfulFrames.Length < 2)
        {
            diagnostics.Add(new PreviewDiagnostic(
                PreviewDiagnosticSeverities.Warning,
                PreviewDiagnosticCategories.Animation,
                "animation_motion_not_available",
                "Animation motion analysis requires at least two successful frame images.",
                details: new Dictionary<string, string>
                {
                    ["successfulFrameCount"] = successfulFrames.Length.ToString(CultureInfo.InvariantCulture),
                    ["requestedFrameCount"] = frames.Count.ToString(CultureInfo.InvariantCulture)
                }));
            return new PreviewAnimationMotionSummary(
                "not_available",
                successfulFrames.Length,
                0,
                0,
                0,
                0,
                new Dictionary<string, string>
                {
                    ["reason"] = "insufficient_successful_frames"
                });
        }

        using var first = SKBitmap.Decode(successfulFrames[0].Render.Value!.FilePath);
        using var last = SKBitmap.Decode(successfulFrames[^1].Render.Value!.FilePath);
        if (first is null || last is null)
        {
            diagnostics.Add(new PreviewDiagnostic(
                PreviewDiagnosticSeverities.Warning,
                PreviewDiagnosticCategories.Animation,
                "animation_motion_not_available",
                "Animation motion analysis could not decode the first or last successful frame image.",
                details: new Dictionary<string, string>
                {
                    ["reason"] = "frame_decode_failed"
                }));
            return new PreviewAnimationMotionSummary(
                "not_available",
                successfulFrames.Length,
                0,
                0,
                0,
                0,
                new Dictionary<string, string>
                {
                    ["reason"] = "frame_decode_failed"
                });
        }

        if (first.Width != last.Width || first.Height != last.Height)
        {
            diagnostics.Add(new PreviewDiagnostic(
                PreviewDiagnosticSeverities.Warning,
                PreviewDiagnosticCategories.Animation,
                "animation_frame_size_mismatch",
                "Animation motion analysis requires same-size first and last frames.",
                details: new Dictionary<string, string>
                {
                    ["firstWidth"] = first.Width.ToString(CultureInfo.InvariantCulture),
                    ["firstHeight"] = first.Height.ToString(CultureInfo.InvariantCulture),
                    ["lastWidth"] = last.Width.ToString(CultureInfo.InvariantCulture),
                    ["lastHeight"] = last.Height.ToString(CultureInfo.InvariantCulture)
                }));
            return new PreviewAnimationMotionSummary(
                "not_available",
                successfulFrames.Length,
                0,
                0,
                0,
                0,
                new Dictionary<string, string>
                {
                    ["reason"] = "frame_size_mismatch"
                });
        }

        var comparison = CompareBitmaps(first, last);
        var changedPercent = comparison.TotalPixels == 0
            ? 0
            : comparison.ChangedPixels * 100d / comparison.TotalPixels;
        var status = comparison.ChangedPixels == 0 ? "static" : "changed";
        diagnostics.Add(new PreviewDiagnostic(
            PreviewDiagnosticSeverities.Info,
            PreviewDiagnosticCategories.Animation,
            comparison.ChangedPixels == 0 ? "animation_static_frames" : "animation_pixels_changed",
            comparison.ChangedPixels == 0
                ? "Sampled animation frames did not change between the first and last successful offsets."
                : "Sampled animation frames changed between the first and last successful offsets.",
            details: new Dictionary<string, string>
            {
                ["firstOffsetMs"] = successfulFrames[0].TimeOffsetMs.ToString(CultureInfo.InvariantCulture),
                ["lastOffsetMs"] = successfulFrames[^1].TimeOffsetMs.ToString(CultureInfo.InvariantCulture),
                ["changedPixels"] = comparison.ChangedPixels.ToString(CultureInfo.InvariantCulture),
                ["totalPixels"] = comparison.TotalPixels.ToString(CultureInfo.InvariantCulture),
                ["changedPercent"] = changedPercent.ToString("0.###", CultureInfo.InvariantCulture),
                ["maxDelta"] = comparison.MaxDelta.ToString(CultureInfo.InvariantCulture),
                ["metadataProvenance"] = "not_available"
            }));

        AddFinalStabilityDiagnostic(successfulFrames, diagnostics);

        return new PreviewAnimationMotionSummary(
            status,
            successfulFrames.Length,
            comparison.ChangedPixels,
            comparison.TotalPixels,
            changedPercent,
            comparison.MaxDelta,
            new Dictionary<string, string>
            {
                ["firstFramePath"] = Path.GetFullPath(successfulFrames[0].Render.Value!.FilePath),
                ["lastFramePath"] = Path.GetFullPath(successfulFrames[^1].Render.Value!.FilePath),
                ["metadataProvenance"] = "not_available"
            });
    }

    private static void AddFinalStabilityDiagnostic(
        IReadOnlyList<PreviewAnimationFrame> successfulFrames,
        List<PreviewDiagnostic> diagnostics)
    {
        if (successfulFrames.Count < 3)
        {
            return;
        }

        using var previous = SKBitmap.Decode(successfulFrames[^2].Render.Value!.FilePath);
        using var final = SKBitmap.Decode(successfulFrames[^1].Render.Value!.FilePath);
        if (previous is null
            || final is null
            || previous.Width != final.Width
            || previous.Height != final.Height)
        {
            return;
        }

        var comparison = CompareBitmaps(previous, final);
        if (comparison.ChangedPixels == 0)
        {
            return;
        }

        var changedPercent = comparison.ChangedPixels * 100d / comparison.TotalPixels;
        diagnostics.Add(new PreviewDiagnostic(
            PreviewDiagnosticSeverities.Warning,
            PreviewDiagnosticCategories.Animation,
            "animation_final_state_unstable",
            "The final sampled frame still differs from the preceding successful frame.",
            details: new Dictionary<string, string>
            {
                ["previousOffsetMs"] = successfulFrames[^2].TimeOffsetMs.ToString(CultureInfo.InvariantCulture),
                ["finalOffsetMs"] = successfulFrames[^1].TimeOffsetMs.ToString(CultureInfo.InvariantCulture),
                ["changedPixels"] = comparison.ChangedPixels.ToString(CultureInfo.InvariantCulture),
                ["changedPercent"] = changedPercent.ToString("0.###", CultureInfo.InvariantCulture),
                ["maxDelta"] = comparison.MaxDelta.ToString(CultureInfo.InvariantCulture)
            }));
    }

    private static BitmapComparison CompareBitmaps(SKBitmap first, SKBitmap second)
    {
        var totalPixels = (long)first.Width * first.Height;
        long changedPixels = 0;
        var maxDelta = 0;

        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                var firstColor = first.GetPixel(x, y);
                var secondColor = second.GetPixel(x, y);
                var delta = Math.Max(
                    Math.Max(
                        Math.Abs(firstColor.Red - secondColor.Red),
                        Math.Abs(firstColor.Green - secondColor.Green)),
                    Math.Max(
                        Math.Abs(firstColor.Blue - secondColor.Blue),
                        Math.Abs(firstColor.Alpha - secondColor.Alpha)));
                if (delta == 0)
                {
                    continue;
                }

                changedPixels++;
                if (delta > maxDelta)
                {
                    maxDelta = delta;
                }
            }
        }

        return new BitmapComparison(changedPixels, totalPixels, maxDelta);
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

    private readonly record struct BitmapComparison(long ChangedPixels, long TotalPixels, int MaxDelta);
}
