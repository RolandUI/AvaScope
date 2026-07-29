using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SessionCapabilitiesResponse
{
    [JsonConstructor]
    public SessionCapabilitiesResponse(
        SessionId sessionId,
        int processId,
        string productVersion,
        ProtocolVersion protocolVersion,
        IReadOnlyList<string> supportedMethods,
        IReadOnlyList<string> inputActions,
        IReadOnlyList<string> automationPatterns,
        IReadOnlyList<RuntimeMutationCapability> mutationCapabilities,
        bool nativePickerSupported,
        string nativePickerMode,
        IReadOnlyList<string> nativePickerOperations,
        string revision)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        if (processId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be positive.");
        }

        if (string.IsNullOrWhiteSpace(productVersion)
            || string.IsNullOrWhiteSpace(nativePickerMode)
            || string.IsNullOrWhiteSpace(revision))
        {
            throw new ArgumentException("Capability version, picker mode, and revision cannot be empty.");
        }

        ProcessId = processId;
        ProductVersion = productVersion;
        ProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
        SupportedMethods = supportedMethods ?? [];
        InputActions = inputActions ?? [];
        AutomationPatterns = automationPatterns ?? [];
        MutationCapabilities = mutationCapabilities ?? [];
        NativePickerSupported = nativePickerSupported;
        NativePickerMode = nativePickerMode;
        NativePickerOperations = nativePickerOperations ?? [];
        Revision = revision;
    }

    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("processId")] public int ProcessId { get; }
    [JsonPropertyName("productVersion")] public string ProductVersion { get; }
    [JsonPropertyName("protocolVersion")] public ProtocolVersion ProtocolVersion { get; }
    [JsonPropertyName("supportedMethods")] public IReadOnlyList<string> SupportedMethods { get; }
    [JsonPropertyName("inputActions")] public IReadOnlyList<string> InputActions { get; }
    [JsonPropertyName("automationPatterns")] public IReadOnlyList<string> AutomationPatterns { get; }
    [JsonPropertyName("mutationCapabilities")] public IReadOnlyList<RuntimeMutationCapability> MutationCapabilities { get; }
    [JsonPropertyName("nativePickerSupported")] public bool NativePickerSupported { get; }
    [JsonPropertyName("nativePickerMode")] public string NativePickerMode { get; }
    [JsonPropertyName("nativePickerOperations")] public IReadOnlyList<string> NativePickerOperations { get; }
    [JsonPropertyName("revision")] public string Revision { get; }

    public static SessionCapabilitiesResponse Current(SessionId sessionId, int processId)
    {
        var methods = BridgeIpcMethods.All;
        var actions = global::AvaScope.Protocol.InputActions.All;
        var patterns = global::AvaScope.Protocol.AutomationPatterns.All;
        var mutations = RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities();
        var pickerMode = OperatingSystem.IsWindows()
            ? "windows_live_and_injected"
            : "injected_only";
        var revisionSource = string.Join(
            "\n",
            [
                AvaScopeProduct.Version,
                AvaScopeProtocol.CurrentVersion.ToString(),
                string.Join(",", methods),
                string.Join(",", actions),
                string.Join(",", patterns),
                string.Join(",", mutations.SelectMany(static item => item.SupportedOperations)),
                string.Join(",", RuntimeMutationPropertyNames.All),
                pickerMode,
                string.Join(",", global::AvaScope.Protocol.NativePickerOperations.All)
            ]);
        var revision = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(revisionSource))).ToLowerInvariant();

        return new SessionCapabilitiesResponse(
            sessionId,
            processId,
            AvaScopeProduct.Version,
            AvaScopeProtocol.CurrentVersion,
            methods,
            actions,
            patterns,
            mutations,
            OperatingSystem.IsWindows(),
            pickerMode,
            global::AvaScope.Protocol.NativePickerOperations.All,
            revision);
    }
}

public static class AutomationPatterns
{
    public const string Invoke = "Invoke";
    public const string SelectionItem = "SelectionItem";
    public const string Toggle = "Toggle";
    public const string ExpandCollapse = "ExpandCollapse";

    public static IReadOnlyList<string> All { get; } =
    [
        Invoke,
        SelectionItem,
        Toggle,
        ExpandCollapse
    ];
}
