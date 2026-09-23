using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeExpression
{
    [JsonConstructor]
    public RuntimeExpression(string kind, string? source = null, JsonElement? literal = null, IReadOnlyList<RuntimeExpression>? operands = null)
    {
        var children = (operands ?? []).ToArray();
        var arity = kind switch
        {
            "literal" or "value" or "count" or "count_true" or "sum" or "minimum" or "maximum" => 0,
            "not" or "number" => 1,
            "eq" or "ne" or "gt" or "ge" or "lt" or "le" or "add" or "subtract" => 2,
            "all" or "any" => -1,
            _ => throw new ArgumentException("Unsupported closed expression operator.", nameof(kind))
        };
        if (arity >= 0 && children.Length != arity || arity == -1 && children.Length is < 1 or > 8 || children.Any(child => child is null))
            throw new ArgumentException("Invalid expression operand count.");
        var sourceOperator = arity == 0 && kind != "literal";
        if (sourceOperator != (source is not null) || source?.Length > 64 || kind == "literal" != literal.HasValue)
            throw new ArgumentException("Only source operators accept source; only literal accepts a literal value.");
        if (literal is { } value && (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.String or JsonValueKind.Number)
            || value.ValueKind == JsonValueKind.String && value.GetString()!.Length > 512
            || value.ValueKind == JsonValueKind.Number && !value.TryGetDecimal(out _)))
            throw new ArgumentException("Literals must be booleans, strings of at most 512 characters or finite decimal JSON numbers.");
        Kind = kind; Source = source; Literal = literal?.Clone(); Operands = children;
    }
    [JsonPropertyName("kind")] public string Kind { get; }
    [JsonPropertyName("source")] public string? Source { get; }
    [JsonPropertyName("literal")] public JsonElement? Literal { get; }
    [JsonPropertyName("operands")] public IReadOnlyList<RuntimeExpression> Operands { get; }
}

public sealed record RuntimeExpressionSource
{
    [JsonConstructor]
    public RuntimeExpressionSource(string id, SemanticWorkflowSelector selector, string attribute = "nodeType")
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("Source ids contain 1..64 ASCII letters, digits or underscores.");
        // Reuse the exact selector and projection validation of query_nodes.
        _ = new RuntimeQueryRequest(new("validation"), "validation", selector, [attribute]);
        Id = id; Selector = selector; Attribute = attribute;
    }
    [JsonPropertyName("id")] public string Id { get; }
    [JsonPropertyName("selector")] public SemanticWorkflowSelector Selector { get; }
    [JsonPropertyName("attribute")] public string Attribute { get; }
}

public sealed record RuntimeExpressionDefinition
{
    [JsonConstructor]
    public RuntimeExpressionDefinition(RuntimeExpression expression, IReadOnlyList<RuntimeExpressionSource>? sources = null,
        int maxNodes = 1024, int maxResults = 64, int maxDepth = 32)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Sources = (sources ?? []).ToArray();
        if (Sources.Count > 8 || Sources.Any(source => source is null) || Sources.Select(source => source.Id).Distinct(StringComparer.Ordinal).Count() != Sources.Count)
            throw new ArgumentException("Select at most eight uniquely named sources.");
        if (maxNodes is < 1 or > 2048 || maxResults is < 1 or > 64 || maxDepth is < 0 or > 32)
            throw new ArgumentException("Expression queries allow 1..2048 nodes, 1..64 results and depth 0..32 per source.");
        var count = 0;
        var used = new HashSet<string>(StringComparer.Ordinal);
        Visit(expression, 0);
        if (Sources.Any(source => !used.Contains(source.Id))) throw new ArgumentException("Every supplied source must be referenced by the expression.");
        MaxNodes = maxNodes; MaxResults = maxResults; MaxDepth = maxDepth;
        void Visit(RuntimeExpression node, int depth)
        {
            if (++count > 48 || depth > 8) throw new ArgumentException("Expressions allow 48 operators and depth 8.");
            if (node.Source is { } id)
            {
                if (!Sources.Any(source => source.Id == id)) throw new ArgumentException("Expression refers to an undefined source.");
                used.Add(id);
            }
            foreach (var child in node.Operands) Visit(child, depth + 1);
        }
    }
    [JsonPropertyName("expression")] public RuntimeExpression Expression { get; }
    [JsonPropertyName("sources")] public IReadOnlyList<RuntimeExpressionSource> Sources { get; }
    [JsonPropertyName("maxNodes")] public int MaxNodes { get; }
    [JsonPropertyName("maxResults")] public int MaxResults { get; }
    [JsonPropertyName("maxDepth")] public int MaxDepth { get; }
}

public sealed record RuntimeExpressionRequest
{
    [JsonConstructor]
    public RuntimeExpressionRequest(SessionId sessionId, string topLevelId, RuntimeExpressionDefinition definition,
        bool requireTrue = false, RuntimeEvidencePolicy? policy = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        if (string.IsNullOrWhiteSpace(topLevelId) || topLevelId.Length > 256) throw new ArgumentException("Select one explicit window.");
        TopLevelId = topLevelId; Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        RequireTrue = requireTrue; Policy = policy;
    }
    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("topLevelId")] public string TopLevelId { get; }
    [JsonPropertyName("definition")] public RuntimeExpressionDefinition Definition { get; }
    [JsonPropertyName("requireTrue")] public bool RequireTrue { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeExpressionValue(
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("observation")] RuntimeQueryValue Observation);

public sealed record RuntimeExpressionSourceObservation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("selector")] SemanticWorkflowSelector Selector,
    [property: JsonPropertyName("attribute")] string Attribute,
    [property: JsonPropertyName("coverage")] RuntimeQueryCoverage Coverage,
    [property: JsonPropertyName("values")] IReadOnlyList<RuntimeExpressionValue> Values);

public sealed record RuntimeExpressionResult(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] JsonElement? Value,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("operands")] IReadOnlyList<RuntimeExpressionResult> Operands);

public sealed record RuntimeExpressionResponse(
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("topLevelId")] string TopLevelId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("result")] RuntimeExpressionResult Result,
    [property: JsonPropertyName("sources")] IReadOnlyList<RuntimeExpressionSourceObservation> Sources,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("completedAt")] DateTimeOffset CompletedAt,
    [property: JsonPropertyName("consistency")] string Consistency,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics);
