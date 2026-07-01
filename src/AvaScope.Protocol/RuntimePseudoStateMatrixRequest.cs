using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public static class RuntimePseudoStates
{
    public const string Normal = "normal";
    public const string PointerOver = "pointerover";
    public const string Pressed = "pressed";
    public const string Disabled = "disabled";
    public const string Selected = "selected";
    public const string SelectedPointerOver = "selected+pointerover";
    public const string Expanded = "expanded";
    public const string Collapsed = "collapsed";

    public static IReadOnlyList<string> DefaultMatrix { get; } =
    [
        Normal,
        PointerOver,
        Pressed,
        Disabled,
        Selected,
        SelectedPointerOver
    ];
}

public sealed record RuntimePseudoStateMatrixRequest
{
    public const int MaximumStates = 24;

    [JsonConstructor]
    public RuntimePseudoStateMatrixRequest(
        SessionId sessionId,
        string topLevelId,
        RuntimeTargetContext? target = null,
        IReadOnlyList<string>? states = null,
        string? requestId = null,
        string? outputDirectory = null,
        string? contactSheetPath = null,
        int maxDepth = 16,
        string treeKind = TreeKinds.Visual,
        string? nodeId = null,
        string? automationId = null,
        string? name = null,
        string? nodeType = null,
        string? text = null,
        string? selector = null,
        string? path = null,
        double diffTolerance = 0)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (maxDepth < 0 || maxDepth > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Max depth must be between 0 and 64.");
        }

        if (diffTolerance < 0 || diffTolerance > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(diffTolerance), diffTolerance, "Diff tolerance must be between 0 and 255.");
        }

        var normalizedStates = NormalizeStates(states);
        if (normalizedStates.Count == 0)
        {
            throw new ArgumentException("Pseudo-state matrix requires at least one state.", nameof(states));
        }

        if (normalizedStates.Count > MaximumStates)
        {
            throw new ArgumentOutOfRangeException(nameof(states), normalizedStates.Count, $"Pseudo-state matrix supports at most {MaximumStates} states.");
        }

        TopLevelId = topLevelId.Trim();
        Target = target;
        States = normalizedStates;
        RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId.Trim();
        OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? null : System.IO.Path.GetFullPath(outputDirectory);
        ContactSheetPath = string.IsNullOrWhiteSpace(contactSheetPath) ? null : System.IO.Path.GetFullPath(contactSheetPath);
        MaxDepth = maxDepth;
        TreeKind = string.IsNullOrWhiteSpace(treeKind) ? TreeKinds.Visual : treeKind.Trim();
        NodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId.Trim();
        AutomationId = string.IsNullOrWhiteSpace(automationId) ? null : automationId.Trim();
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        NodeType = string.IsNullOrWhiteSpace(nodeType) ? null : nodeType.Trim();
        Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Selector = string.IsNullOrWhiteSpace(selector) ? null : selector.Trim();
        Path = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        DiffTolerance = diffTolerance;
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? Target { get; }

    [JsonPropertyName("states")]
    public IReadOnlyList<string> States { get; }

    [JsonPropertyName("outputDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputDirectory { get; }

    [JsonPropertyName("contactSheetPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContactSheetPath { get; }

    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; }

    [JsonPropertyName("treeKind")]
    public string TreeKind { get; }

    [JsonPropertyName("nodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeId { get; }

    [JsonPropertyName("automationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationId { get; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; }

    [JsonPropertyName("nodeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeType { get; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; }

    [JsonPropertyName("selector")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Selector { get; }

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; }

    [JsonPropertyName("diffTolerance")]
    public double DiffTolerance { get; }

    private static IReadOnlyList<string> NormalizeStates(IReadOnlyList<string>? states)
    {
        return (states is null || states.Count == 0 ? RuntimePseudoStates.DefaultMatrix : states)
            .Where(static state => !string.IsNullOrWhiteSpace(state))
            .Select(static state => string.Join(
                "+",
                state.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(static token => token.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant())))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
