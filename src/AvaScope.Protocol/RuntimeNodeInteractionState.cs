using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeNodeInteractionState
{
    [JsonConstructor]
    public RuntimeNodeInteractionState(
        bool visible,
        bool enabled,
        bool rendered,
        bool actionable,
        IReadOnlyList<string>? availableActions = null)
    {
        Visible = visible;
        Enabled = enabled;
        Rendered = rendered;
        Actionable = actionable;
        AvailableActions = availableActions ?? [];
    }

    [JsonPropertyName("visible")]
    public bool Visible { get; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; }

    [JsonPropertyName("rendered")]
    public bool Rendered { get; }

    [JsonPropertyName("actionable")]
    public bool Actionable { get; }

    [JsonPropertyName("availableActions")]
    public IReadOnlyList<string> AvailableActions { get; }
}
