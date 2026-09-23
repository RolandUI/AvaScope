using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeCustomActionDescriptor
{
    [JsonConstructor]
    public RuntimeCustomActionDescriptor(
        string name,
        RuntimeTargetContext target,
        bool executable,
        string safetyClassification,
        IReadOnlyList<RuntimeCustomActionParameterDescriptor>? parameters = null,
        IReadOnlyDictionary<string, string>? requiredState = null,
        string? description = null,
        string? unavailableReason = null,
        string targetScope = "node",
        RuntimeTestFixtureDescriptor? testFixture = null,
        bool supportsOperations = false,
        bool supportsCancellation = false,
        bool requiresSceneObject = false)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(targetScope))
        {
            throw new ArgumentException("Custom action name and target scope cannot be empty.");
        }

        if (!RuntimeCustomActionSafetyClassifications.All.Contains(safetyClassification, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Custom action safety classification '{safetyClassification}' is not supported.", nameof(safetyClassification));
        }

        Name = name.Trim();
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Executable = executable;
        SafetyClassification = safetyClassification;
        Parameters = (parameters ?? []).Take(32).ToArray();
        RequiredState = (requiredState ?? new Dictionary<string, string>())
            .Take(32)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UnavailableReason = string.IsNullOrWhiteSpace(unavailableReason) ? null : unavailableReason.Trim();
        TargetScope = targetScope.Trim();
        TestFixture = testFixture;
        SupportsOperations = supportsOperations;
        SupportsCancellation = supportsCancellation;
        RequiresSceneObject = requiresSceneObject;
    }

    [JsonPropertyName("name")] public string Name { get; }
    [JsonPropertyName("supportsOperations")] public bool SupportsOperations { get; }
    [JsonPropertyName("supportsCancellation")] public bool SupportsCancellation { get; }
    [JsonPropertyName("requiresSceneObject")] public bool RequiresSceneObject { get; }
    [JsonPropertyName("testFixture")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTestFixtureDescriptor? TestFixture { get; }
    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("targetScope")] public string TargetScope { get; }
    [JsonPropertyName("requiredState")] public IReadOnlyDictionary<string, string> RequiredState { get; }
    [JsonPropertyName("executable")] public bool Executable { get; }
    [JsonPropertyName("parameters")] public IReadOnlyList<RuntimeCustomActionParameterDescriptor> Parameters { get; }
    [JsonPropertyName("safetyClassification")] public string SafetyClassification { get; }
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; }
    [JsonPropertyName("unavailableReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UnavailableReason { get; }
}
