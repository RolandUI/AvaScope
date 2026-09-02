using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeCustomActionRequest
{
    [JsonConstructor]
    public RuntimeCustomActionRequest(
        string requestId,
        RuntimeTargetContext target,
        string actionName,
        IReadOnlyDictionary<string, string>? parameters = null,
        bool allowDestructive = false)
    {
        if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(actionName))
        {
            throw new ArgumentException("Custom action request id and action name cannot be empty.");
        }

        RequestId = requestId.Trim();
        Target = target ?? throw new ArgumentNullException(nameof(target));
        ActionName = actionName.Trim();
        Parameters = (parameters ?? new Dictionary<string, string>())
            .Take(32)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        AllowDestructive = allowDestructive;
    }

    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("actionName")] public string ActionName { get; }
    [JsonPropertyName("parameters")] public IReadOnlyDictionary<string, string> Parameters { get; }
    [JsonPropertyName("allowDestructive")] public bool AllowDestructive { get; }
}
