using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record X11EnvironmentOptions(
    [property: JsonPropertyName("mode")] string Mode = "managed",
    [property: JsonPropertyName("display")] string? Display = null,
    [property: JsonPropertyName("xauthority")] string? Xauthority = null,
    [property: JsonPropertyName("width")] int Width = 1280,
    [property: JsonPropertyName("height")] int Height = 900,
    [property: JsonPropertyName("dpi")] int Dpi = 96,
    [property: JsonPropertyName("windowManager")] bool WindowManager = false,
    [property: JsonPropertyName("sessionBus")] bool SessionBus = false,
    [property: JsonPropertyName("timeoutMs")] int TimeoutMs = 10000);

public sealed record RuntimeEnvironmentEvidence(
    [property: JsonPropertyName("backend")] string Backend,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("display")] string? Display,
    [property: JsonPropertyName("geometry")] string? Geometry,
    [property: JsonPropertyName("tcpListening")] string TcpListening,
    [property: JsonPropertyName("runtimeDirectory")] string? RuntimeDirectory,
    [property: JsonPropertyName("runtimeDirectoryRemoved")] bool RuntimeDirectoryRemoved,
    [property: JsonPropertyName("helpers")] IReadOnlyList<RuntimeHelperEvidence> Helpers,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics,
    [property: JsonPropertyName("socketTransport")] string SocketTransport = "unknown");

public sealed record RuntimeHelperEvidence(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("exited")] bool Exited,
    [property: JsonPropertyName("stdoutPath")] string StdoutPath,
    [property: JsonPropertyName("stderrPath")] string StderrPath);
