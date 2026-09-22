using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeTestFixtureDescriptor
{
    [JsonConstructor]
    public RuntimeTestFixtureDescriptor(string name, string version, IReadOnlyList<string> resourceIds,
        SemanticWaitCondition readiness, SemanticWorkflowSelector? readinessSelector = null, bool hasCleanup = true)
    {
        ValidateIdentifier(name, nameof(name));
        ValidateIdentifier(version, nameof(version));
        if (resourceIds is null || resourceIds.Count is < 1 or > 16) throw new ArgumentException("Declare 1–16 test-resource identities.", nameof(resourceIds));
        foreach (var resource in resourceIds) ValidateIdentifier(resource, nameof(resourceIds));
        Name = name;
        Version = version;
        ResourceIds = resourceIds.Distinct(StringComparer.Ordinal).ToArray();
        Readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
        ReadinessSelector = readinessSelector;
        HasCleanup = hasCleanup;
    }

    [JsonPropertyName("name")] public string Name { get; }
    [JsonPropertyName("version")] public string Version { get; }
    [JsonPropertyName("resourceIds")] public IReadOnlyList<string> ResourceIds { get; }
    [JsonPropertyName("readiness")] public SemanticWaitCondition Readiness { get; }
    [JsonPropertyName("readinessSelector")] public SemanticWorkflowSelector? ReadinessSelector { get; }
    [JsonPropertyName("hasCleanup")] public bool HasCleanup { get; }
    [JsonPropertyName("prepareAction")] public string PrepareAction => "fixture.prepare." + Name;
    [JsonPropertyName("cleanupAction")] public string? CleanupAction => HasCleanup ? "fixture.cleanup." + Name : null;

    public static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64
            || !value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'))
            throw new ArgumentException("Fixture/resource identifiers require 1–64 ASCII letters, digits, '.', '-' or '_'; paths are not resource identities.", parameterName);
    }
}

public sealed record RuntimeScenarioFixtureOptions
{
    [JsonConstructor]
    public RuntimeScenarioFixtureOptions(string name, string resourceId, string targetAutomationId,
        IReadOnlyDictionary<string, string>? parameters = null, int timeoutMs = 5000, int cleanupTimeoutMs = 3000)
    {
        RuntimeTestFixtureDescriptor.ValidateIdentifier(name, nameof(name));
        RuntimeTestFixtureDescriptor.ValidateIdentifier(resourceId, nameof(resourceId));
        if (string.IsNullOrWhiteSpace(targetAutomationId) || targetAutomationId.Length > 256)
            throw new ArgumentException("Fixture target requires a bounded explicit AutomationId.", nameof(targetAutomationId));
        if (parameters is { Count: > 16 } || parameters?.Any(pair => pair.Key == "testResource" || pair.Key.Length > 64 || pair.Value is null || pair.Value.Length > 256) == true)
            throw new ArgumentException("Fixture parameters are bounded and cannot replace the reserved testResource identity.", nameof(parameters));
        if (timeoutMs is < 50 or > 60000) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        if (cleanupTimeoutMs is < 50 or > 10000) throw new ArgumentOutOfRangeException(nameof(cleanupTimeoutMs));
        Name = name;
        ResourceId = resourceId;
        TargetAutomationId = targetAutomationId;
        Parameters = parameters is null ? new Dictionary<string, string>() : new Dictionary<string, string>(parameters, StringComparer.Ordinal);
        TimeoutMs = timeoutMs;
        CleanupTimeoutMs = cleanupTimeoutMs;
    }
    [JsonPropertyName("name")] public string Name { get; }
    [JsonPropertyName("resourceId")] public string ResourceId { get; }
    [JsonPropertyName("targetAutomationId")] public string TargetAutomationId { get; }
    [JsonPropertyName("parameters")] public IReadOnlyDictionary<string, string> Parameters { get; }
    [JsonPropertyName("timeoutMs")] public int TimeoutMs { get; }
    [JsonPropertyName("cleanupTimeoutMs")] public int CleanupTimeoutMs { get; }
}

public sealed record RuntimeTestFixtureEvidence(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("resourceId")] string ResourceId,
    [property: JsonPropertyName("parameterNames")] IReadOnlyList<string> ParameterNames,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("version")] string? Version = null,
    [property: JsonPropertyName("preparationStatus")] string PreparationStatus = "not_started",
    [property: JsonPropertyName("readinessStatus")] string ReadinessStatus = "not_started",
    [property: JsonPropertyName("cleanupStatus")] string CleanupStatus = "not_started",
    [property: JsonPropertyName("completedAt")] DateTimeOffset? CompletedAt = null,
    [property: JsonPropertyName("readinessObservation")] RuntimeWaitObservation? ReadinessObservation = null,
    [property: JsonPropertyName("provenance")] string Provenance = "explicit_host_fixture_allowlisted_custom_actions");
