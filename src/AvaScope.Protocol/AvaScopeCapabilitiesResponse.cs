using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AvaScopeCapabilitiesResponse
{
    [JsonConstructor]
    public AvaScopeCapabilitiesResponse(
        string serviceName,
        ProtocolVersion protocolVersion,
        DateTimeOffset generatedAt,
        IReadOnlyDictionary<string, string> compatibilityPolicy,
        IReadOnlyList<AvaScopeCapability> capabilities,
        IReadOnlyList<AvaScopeToolCapability> tools,
        IReadOnlyList<RuntimeMutationCapability> runtimeMutationCapabilities,
        IReadOnlyList<ProtocolError>? diagnostics = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));
        }

        ServiceName = serviceName.Trim();
        ProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
        GeneratedAt = generatedAt;
        CompatibilityPolicy = compatibilityPolicy ?? new Dictionary<string, string>();
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        Tools = tools ?? throw new ArgumentNullException(nameof(tools));
        RuntimeMutationCapabilities = runtimeMutationCapabilities ?? throw new ArgumentNullException(nameof(runtimeMutationCapabilities));
        Diagnostics = diagnostics ?? [];
    }

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; }

    [JsonPropertyName("protocolVersion")]
    public ProtocolVersion ProtocolVersion { get; }

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; }

    [JsonPropertyName("compatibilityPolicy")]
    public IReadOnlyDictionary<string, string> CompatibilityPolicy { get; }

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<AvaScopeCapability> Capabilities { get; }

    [JsonPropertyName("tools")]
    public IReadOnlyList<AvaScopeToolCapability> Tools { get; }

    [JsonPropertyName("runtimeMutationCapabilities")]
    public IReadOnlyList<RuntimeMutationCapability> RuntimeMutationCapabilities { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }
}
