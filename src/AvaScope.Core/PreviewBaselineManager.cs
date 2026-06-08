using System.Globalization;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewBaselineManager
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly PreviewHostClient _previewHostClient;
    private readonly PreviewImageDiffer _imageDiffer;
    private readonly TimeProvider _timeProvider;

    public PreviewBaselineManager(PreviewHostClient previewHostClient)
        : this(previewHostClient, new PreviewImageDiffer(), TimeProvider.System)
    {
    }

    public PreviewBaselineManager(
        PreviewHostClient previewHostClient,
        PreviewImageDiffer imageDiffer,
        TimeProvider timeProvider)
    {
        _previewHostClient = previewHostClient ?? throw new ArgumentNullException(nameof(previewHostClient));
        _imageDiffer = imageDiffer ?? throw new ArgumentNullException(nameof(imageDiffer));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<CoreResult<PreviewBaselineCreateResponse>> CreateAsync(
        PreviewRequest request,
        IReadOnlyList<PreviewViewport> viewports,
        string manifestPath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(viewports);

        if (viewports.Count == 0)
        {
            return CoreResult<PreviewBaselineCreateResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidPreviewRequest,
                "At least one baseline viewport size is required."));
        }

        var fullManifestPath = Path.GetFullPath(manifestPath);
        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        var baseOutputPath = Path.Combine(fullOutputDirectory, "baseline.png");
        var baselineRequest = CreateRequestForOutput(request, baseOutputPath, width: null, height: null);
        var render = await _previewHostClient.RenderBatchAsync(
            baselineRequest,
            viewports,
            cancellationToken: cancellationToken);
        if (!render.Success)
        {
            return CoreResult<PreviewBaselineCreateResponse>.Fail(render.Error!);
        }

        var failedEntry = render.Value!.Entries.FirstOrDefault(static entry => !entry.Render.Success);
        if (failedEntry is not null)
        {
            return CoreResult<PreviewBaselineCreateResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewBaselineFailed,
                $"Baseline render failed for viewport {failedEntry.Viewport.Width.ToString(CultureInfo.InvariantCulture)}x{failedEntry.Viewport.Height.ToString(CultureInfo.InvariantCulture)}.",
                failedEntry.Render.Error?.Details));
        }

        var entries = render.Value.Entries
            .Select((entry, index) => new PreviewBaselineEntry(
                index,
                entry.Viewport,
                entry.Render.Value!.FilePath,
                request.Dpi,
                request.ProjectPath,
                request.ViewPath,
                request.ThemeVariant,
                request.Culture,
                request.DesignDataType))
            .ToArray();
        var manifest = new PreviewBaselineManifest(
            PreviewBaselineManifest.CurrentVersion,
            _timeProvider.GetUtcNow(),
            entries);

        try
        {
            var manifestDirectory = Path.GetDirectoryName(fullManifestPath);
            if (!string.IsNullOrWhiteSpace(manifestDirectory))
            {
                Directory.CreateDirectory(manifestDirectory);
            }

            File.WriteAllText(fullManifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return CoreResult<PreviewBaselineCreateResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewBaselineFailed,
                $"Baseline manifest could not be written: {exception.Message}"));
        }

        return CoreResult<PreviewBaselineCreateResponse>.Ok(new PreviewBaselineCreateResponse(
            fullManifestPath,
            manifest,
            render.Value));
    }

    public async Task<CoreResult<PreviewBaselineCheckResponse>> CheckAsync(
        string manifestPath,
        string outputDirectory,
        string diffDirectory,
        double tolerance,
        CancellationToken cancellationToken = default)
    {
        if (tolerance < 0 || tolerance > 255)
        {
            return CoreResult<PreviewBaselineCheckResponse>.Fail(new CoreError(
                CoreErrorCodes.ImageDiffFailed,
                "Tolerance must be between 0 and 255."));
        }

        var fullManifestPath = Path.GetFullPath(manifestPath);
        var manifestResult = LoadManifest(fullManifestPath);
        if (!manifestResult.Success)
        {
            return CoreResult<PreviewBaselineCheckResponse>.Fail(manifestResult.Error!);
        }

        var manifest = manifestResult.Value!;
        var entries = new List<PreviewBaselineCheckEntry>(manifest.Entries.Count);
        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        var fullDiffDirectory = Path.GetFullPath(diffDirectory);
        Directory.CreateDirectory(fullOutputDirectory);
        Directory.CreateDirectory(fullDiffDirectory);

        var passed = true;
        foreach (var baseline in manifest.Entries)
        {
            var token = CreateVariantToken(baseline.Index, baseline.Viewport);
            var currentPath = Path.Combine(fullOutputDirectory, $"current-{token}.png");
            var diffPath = Path.Combine(fullDiffDirectory, $"diff-{token}.png");
            var request = new PreviewRequest(
                currentPath,
                baseline.Viewport.Width,
                baseline.Viewport.Height,
                baseline.Dpi,
                baseline.ProjectPath,
                baseline.ViewPath,
                baseline.ThemeVariant,
                baseline.Culture,
                baseline.DesignDataType);
            var render = await _previewHostClient.RenderAsync(request, cancellationToken);
            var renderResult = ToToolResult(render);
            ToolResult<PreviewDiffResponse> diffResult;
            if (render.Success)
            {
                var diff = _imageDiffer.Compare(
                    baseline.ImagePath,
                    render.Value!.FilePath,
                    diffPath,
                    tolerance);
                diffResult = ToToolResult(diff);
                if (!diff.Success || !diff.Value!.Passed)
                {
                    passed = false;
                }
            }
            else
            {
                passed = false;
                diffResult = ToolResult<PreviewDiffResponse>.Fail(new ProtocolError(
                    render.Error!.Code,
                    render.Error.Message,
                    render.Error.Details));
            }

            entries.Add(new PreviewBaselineCheckEntry(
                baseline,
                currentPath,
                diffPath,
                renderResult,
                diffResult));
        }

        return CoreResult<PreviewBaselineCheckResponse>.Ok(new PreviewBaselineCheckResponse(
            fullManifestPath,
            passed,
            entries,
            _timeProvider.GetUtcNow()));
    }

    private static CoreResult<PreviewBaselineManifest> LoadManifest(string manifestPath)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<PreviewBaselineManifest>(
                File.ReadAllText(manifestPath),
                JsonOptions);
            if (manifest is null)
            {
                return CoreResult<PreviewBaselineManifest>.Fail(new CoreError(
                    CoreErrorCodes.PreviewBaselineManifestInvalid,
                    "Baseline manifest did not contain a manifest object."));
            }

            if (manifest.Entries.Count == 0)
            {
                return CoreResult<PreviewBaselineManifest>.Fail(new CoreError(
                    CoreErrorCodes.PreviewBaselineManifestInvalid,
                    "Baseline manifest must contain at least one entry."));
            }

            return CoreResult<PreviewBaselineManifest>.Ok(manifest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return CoreResult<PreviewBaselineManifest>.Fail(new CoreError(
                CoreErrorCodes.PreviewBaselineManifestInvalid,
                $"Baseline manifest could not be loaded: {exception.Message}"));
        }
    }

    private static PreviewRequest CreateRequestForOutput(
        PreviewRequest request,
        string outputPath,
        double? width,
        double? height)
    {
        return new PreviewRequest(
            outputPath,
            width,
            height,
            request.Dpi,
            request.ProjectPath,
            request.ViewPath,
            request.ThemeVariant,
            request.Culture,
            request.DesignDataType);
    }

    private static ToolResult<T> ToToolResult<T>(CoreResult<T> result)
    {
        return result.Success
            ? ToolResult<T>.Ok(result.Value!)
            : ToolResult<T>.Fail(new ProtocolError(
                result.Error!.Code,
                result.Error.Message,
                result.Error.Details));
    }

    private static string CreateVariantToken(int index, PreviewViewport viewport)
    {
        return $"{index + 1:00}-{FormatSize(viewport.Width)}x{FormatSize(viewport.Height)}";
    }

    private static string FormatSize(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', '_');
    }
}
