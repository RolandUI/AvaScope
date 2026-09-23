using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record WaylandEnvironmentOptions(
    [property: JsonPropertyName("lane")] string Lane = "native_wayland",
    [property: JsonPropertyName("width")] int Width = 1280,
    [property: JsonPropertyName("height")] int Height = 900,
    [property: JsonPropertyName("scale")] int Scale = 1,
    [property: JsonPropertyName("keyboardLayout")] string KeyboardLayout = "us",
    [property: JsonPropertyName("keyboardVariant")] string KeyboardVariant = "",
    [property: JsonPropertyName("timeoutMs")] int TimeoutMs = 10000);

public sealed record WaylandEnvironmentEvidence(
    [property: JsonPropertyName("lane")] string Lane,
    [property: JsonPropertyName("compositorVersion")] string? CompositorVersion,
    [property: JsonPropertyName("renderer")] string Renderer,
    [property: JsonPropertyName("logicalWidth")] int LogicalWidth,
    [property: JsonPropertyName("logicalHeight")] int LogicalHeight,
    [property: JsonPropertyName("scale")] int Scale,
    [property: JsonPropertyName("outputPixelWidth")] int? OutputPixelWidth,
    [property: JsonPropertyName("outputPixelHeight")] int? OutputPixelHeight,
    [property: JsonPropertyName("keyboardLayout")] string KeyboardLayout,
    [property: JsonPropertyName("keyboardVariant")] string KeyboardVariant,
    [property: JsonPropertyName("keyboardEvidence")] string KeyboardEvidence,
    [property: JsonPropertyName("limitations")] IReadOnlyList<string> Limitations);
