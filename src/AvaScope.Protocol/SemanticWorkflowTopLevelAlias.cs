using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowTopLevelAlias
{
    [JsonConstructor]
    public SemanticWorkflowTopLevelAlias(string alias, SemanticTopLevelSelector selector)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("Top-level alias cannot be empty.", nameof(alias));
        }

        Alias = alias.Trim();
        Selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    [JsonPropertyName("alias")]
    public string Alias { get; }

    [JsonPropertyName("selector")]
    public SemanticTopLevelSelector Selector { get; }
}
