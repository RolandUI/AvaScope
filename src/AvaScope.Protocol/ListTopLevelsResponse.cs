using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ListTopLevelsResponse
{
    [JsonConstructor]
    public ListTopLevelsResponse(IReadOnlyList<TopLevelSummary>? topLevels = null)
    {
        TopLevels = topLevels ?? Array.Empty<TopLevelSummary>();
    }

    [JsonPropertyName("topLevels")]
    public IReadOnlyList<TopLevelSummary> TopLevels { get; }
}
