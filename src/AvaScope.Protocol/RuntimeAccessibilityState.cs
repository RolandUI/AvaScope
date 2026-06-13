using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeAccessibilityState
{
    [JsonConstructor]
    public RuntimeAccessibilityState(
        string provenance,
        string? automationName = null,
        string? automationHelpText = null,
        string? accessKey = null,
        string? labeledBy = null,
        string? controlType = null,
        bool? focusable = null,
        bool? isTabStop = null,
        int? tabIndex = null,
        bool? isEnabled = null)
    {
        if (string.IsNullOrWhiteSpace(provenance))
        {
            throw new ArgumentException("Accessibility provenance cannot be empty.", nameof(provenance));
        }

        Provenance = provenance.Trim();
        AutomationName = Normalize(automationName);
        AutomationHelpText = Normalize(automationHelpText);
        AccessKey = Normalize(accessKey);
        LabeledBy = Normalize(labeledBy);
        ControlType = Normalize(controlType);
        Focusable = focusable;
        IsTabStop = isTabStop;
        TabIndex = tabIndex;
        IsEnabled = isEnabled;
    }

    [JsonPropertyName("provenance")]
    public string Provenance { get; }

    [JsonPropertyName("automationName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationName { get; }

    [JsonPropertyName("automationHelpText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationHelpText { get; }

    [JsonPropertyName("accessKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccessKey { get; }

    [JsonPropertyName("labeledBy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LabeledBy { get; }

    [JsonPropertyName("controlType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ControlType { get; }

    [JsonPropertyName("focusable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Focusable { get; }

    [JsonPropertyName("isTabStop")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsTabStop { get; }

    [JsonPropertyName("tabIndex")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TabIndex { get; }

    [JsonPropertyName("isEnabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsEnabled { get; }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
