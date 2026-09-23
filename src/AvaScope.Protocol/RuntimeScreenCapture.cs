using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeScreenCaptureRequest
{
    [JsonConstructor]
    public RuntimeScreenCaptureRequest(RuntimeTargetContext target, string outputDirectory, string mode = "paired",
        string? desktopScope = null, int timeoutMs = 3000, RuntimeEvidencePolicy? policy = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (target.NodeId is not null || target.TargetKind != "top_level" || string.IsNullOrEmpty(target.TopLevelGeneration))
            throw new ArgumentException("Select an observed top-level with its current generation.");
        if (mode is not ("paired" or "rendered" or "native")) throw new ArgumentException("Use paired, rendered or native capture.");
        if (desktopScope is not null and not "declared_test_desktop") throw new ArgumentException("Only a host-authorized declared_test_desktop scope is supported.");
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Path.IsPathFullyQualified(outputDirectory))
            throw new ArgumentException("Use an absolute evidence output directory.");
        if (timeoutMs is < 250 or > 5000) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        OutputDirectory = Path.GetFullPath(outputDirectory); Mode = mode; DesktopScope = desktopScope; TimeoutMs = timeoutMs; Policy = policy;
    }
    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("outputDirectory")] public string OutputDirectory { get; }
    [JsonPropertyName("mode")] public string Mode { get; }
    [JsonPropertyName("desktopScope")] public string? DesktopScope { get; }
    [JsonPropertyName("timeoutMs")] public int TimeoutMs { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeScreenFrame(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("pixelWidth")] int PixelWidth,
    [property: JsonPropertyName("pixelHeight")] int PixelHeight,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("completedAt")] DateTimeOffset CompletedAt,
    [property: JsonPropertyName("desktopBounds")] NodeBounds? DesktopBounds,
    [property: JsonPropertyName("desktopUnits")] string? DesktopUnits,
    [property: JsonPropertyName("nativePixelSize")] RuntimeSize? NativePixelSize,
    [property: JsonPropertyName("visibleImageRegions")] IReadOnlyList<NodeBounds> VisibleImageRegions,
    [property: JsonPropertyName("masking")] string Masking,
    [property: JsonPropertyName("filePath")] string? FilePath,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics,
    [property: JsonPropertyName("png"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte[]? Png = null,
    [property: JsonPropertyName("nativeRegions")] IReadOnlyList<RuntimeScreenRegion>? NativeRegions = null);

public sealed record RuntimeScreenRegion(
    [property: JsonPropertyName("desktopBounds")] NodeBounds DesktopBounds,
    [property: JsonPropertyName("nativePixelSize")] RuntimeSize NativePixelSize,
    [property: JsonPropertyName("imageBounds")] NodeBounds ImageBounds);

public sealed record RuntimeScreenComparison(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("comparedPixels")] long ComparedPixels,
    [property: JsonPropertyName("differentPixels")] long DifferentPixels,
    [property: JsonPropertyName("channelTolerance")] int ChannelTolerance,
    [property: JsonPropertyName("interpretation")] string Interpretation);

public sealed record RuntimeScreenCaptureResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("backend")] RuntimeBackendInfo Backend,
    [property: JsonPropertyName("renderScaling")] double RenderScaling,
    [property: JsonPropertyName("desktopScaling")] double DesktopScaling,
    [property: JsonPropertyName("consistency")] string Consistency,
    [property: JsonPropertyName("rendered")] RuntimeScreenFrame? Rendered,
    [property: JsonPropertyName("native")] RuntimeScreenFrame? Native,
    [property: JsonPropertyName("comparison")] RuntimeScreenComparison? Comparison,
    [property: JsonPropertyName("limitations")] IReadOnlyList<string> Limitations);
