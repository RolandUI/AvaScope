using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineMutationPreset
{
    [JsonConstructor]
    public PreviewBaselineMutationPreset(
        string id,
        string? description = null,
        IReadOnlyList<RuntimeMutationOperation>? operations = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Mutation preset id cannot be empty.", nameof(id));
        }

        Id = id;
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
        Operations = operations ?? [];
    }

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; }

    [JsonPropertyName("operations")]
    public IReadOnlyList<RuntimeMutationOperation> Operations { get; }
}
