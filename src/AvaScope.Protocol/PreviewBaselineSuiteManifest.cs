using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineSuiteManifest
{
    public const int CurrentVersion = 1;

    [JsonConstructor]
    public PreviewBaselineSuiteManifest(
        int version,
        string name,
        IReadOnlyList<PreviewBaselineSuiteEntry>? entries = null,
        PreviewBaselineSuiteDefaults? defaults = null,
        IReadOnlyList<PreviewBaselineMutationPreset>? mutationPresets = null)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Baseline suite manifest version must be positive.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Baseline suite name cannot be empty.", nameof(name));
        }

        Version = version;
        Name = name;
        Defaults = defaults;
        Entries = entries ?? [];
        MutationPresets = mutationPresets ?? [];
    }

    [JsonPropertyName("version")]
    public int Version { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("defaults")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreviewBaselineSuiteDefaults? Defaults { get; }

    [JsonPropertyName("entries")]
    public IReadOnlyList<PreviewBaselineSuiteEntry> Entries { get; }

    [JsonPropertyName("mutationPresets")]
    public IReadOnlyList<PreviewBaselineMutationPreset> MutationPresets { get; }
}
