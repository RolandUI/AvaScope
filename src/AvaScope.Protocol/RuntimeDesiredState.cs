using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeDesiredStateRequest
{
    public static IReadOnlyList<string> Properties { get; } = ["checked", "expanded", "text", "value", "selection"];

    [JsonConstructor]
    public RuntimeDesiredStateRequest(RuntimeTargetContext target, string property, JsonElement desired, string requestId,
        RuntimeEvidencePolicy? policy = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        ValidateTarget(target);
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128) throw new ArgumentException("A request id of 1..128 characters is required.");
        if (!Properties.Contains(property, StringComparer.Ordinal)) throw new ArgumentException("Desired state must be checked, expanded, text, value or selection.");
        var valid = property switch
        {
            "checked" => desired.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null,
            "expanded" => desired.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "text" => desired.ValueKind == JsonValueKind.String && desired.GetString()!.Length <= 4096,
            "value" => desired.ValueKind == JsonValueKind.Number && desired.TryGetDouble(out var number) && double.IsFinite(number),
            "selection" => desired.ValueKind == JsonValueKind.Array && desired.GetArrayLength() <= 32,
            _ => false
        };
        if (!valid) throw new ArgumentException("Desired state has the wrong type or exceeds its bound (text: 4096 characters; selection: 32 targets).");
        if (property == "selection")
        {
            var targets = desired.Deserialize<RuntimeTargetContext[]>()!;
            foreach (var item in targets)
            {
                if (item is null) throw new ArgumentException("Selection items must be explicit targets.");
                ValidateTarget(item);
                if (item.SessionId != target.SessionId || item.TopLevelId != target.TopLevelId)
                    throw new ArgumentException("Selection targets must belong to the selected session and window.");
            }
            if (targets.Select(item => item.NodeId).Distinct(StringComparer.Ordinal).Count() != targets.Length)
                throw new ArgumentException("Selection targets must be distinct.");
        }
        Property = property; Desired = desired.Clone(); RequestId = requestId; Policy = policy;
    }

    private static void ValidateTarget(RuntimeTargetContext target)
    {
        if (target.TreeKind != TreeKinds.Visual || string.IsNullOrEmpty(target.NodeId)
            || string.IsNullOrEmpty(target.NodeGeneration) || string.IsNullOrEmpty(target.TopLevelGeneration))
            throw new ArgumentException("Desired-state operations require a fresh visual node target with both generation tokens.");
    }

    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("property")] public string Property { get; }
    [JsonPropertyName("desired")] public JsonElement Desired { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeDesiredStateValue(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] JsonElement? Value);

public sealed record RuntimeDesiredStateResponse(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("property")] string Property,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("verified")] bool Verified,
    [property: JsonPropertyName("before")] RuntimeDesiredStateValue Before,
    [property: JsonPropertyName("after")] RuntimeDesiredStateValue After,
    [property: JsonPropertyName("dispatchedOperations")] int DispatchedOperations,
    [property: JsonPropertyName("preparationPerformed")] bool PreparationPerformed,
    [property: JsonPropertyName("provenance")] RuntimeOperationProvenance Provenance,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("replayed")] bool Replayed = false);
