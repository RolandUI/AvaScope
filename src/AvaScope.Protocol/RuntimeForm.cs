using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeFormInspectionRequest
{
    [JsonConstructor]
    public RuntimeFormInspectionRequest(SessionId sessionId, string topLevelId, SemanticWorkflowSelector? scope = null,
        int maxFields = 24, int maxNodes = 512, RuntimeEvidencePolicy? policy = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        if (string.IsNullOrWhiteSpace(topLevelId) || topLevelId.Length > 256) throw new ArgumentException("Select an explicit top-level id of at most 256 characters.");
        if (maxFields is < 1 or > 32 || maxNodes is < 1 or > 2048) throw new ArgumentException("Form inspection allows 1..32 fields and 1..2048 nodes.");
        if (scope is not null)
        {
            _ = new RuntimeQueryRequest(sessionId, topLevelId, scope, maxDepth: 32);
            if (scope.TreeKind != TreeKinds.Visual) throw new ArgumentException("Form scope must use the visual tree.");
        }
        TopLevelId = topLevelId; Scope = scope; MaxFields = maxFields; MaxNodes = maxNodes; Policy = policy;
    }
    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("topLevelId")] public string TopLevelId { get; }
    [JsonPropertyName("scope")] public SemanticWorkflowSelector? Scope { get; }
    [JsonPropertyName("maxFields")] public int MaxFields { get; }
    [JsonPropertyName("maxNodes")] public int MaxNodes { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeFormFieldInput
{
    [JsonConstructor]
    public RuntimeFormFieldInput(string id, SemanticWorkflowSelector selector, JsonElement desired)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64) throw new ArgumentException("A field mapping id of 1..64 characters is required.");
        Selector = selector ?? throw new ArgumentNullException(nameof(selector));
        if (desired.ValueKind is JsonValueKind.Undefined or JsonValueKind.Object || desired.GetRawText().Length > 16384)
            throw new ArgumentException("Field values must be bounded scalar values or selection target arrays.");
        Id = id; Desired = desired.Clone();
    }
    [JsonPropertyName("id")] public string Id { get; }
    [JsonPropertyName("selector")] public SemanticWorkflowSelector Selector { get; }
    [JsonPropertyName("desired")] public JsonElement Desired { get; }
}

public sealed record RuntimeFormFillRequest
{
    [JsonConstructor]
    public RuntimeFormFillRequest(RuntimeFormInspectionRequest form, IReadOnlyList<RuntimeFormFieldInput> fields, string requestId,
        int settleMs = 100, bool allowSensitiveInput = false)
    {
        Form = form ?? throw new ArgumentNullException(nameof(form));
        Fields = fields?.ToArray() ?? throw new ArgumentNullException(nameof(fields));
        if (fields.Count is < 1 or > 16 || fields.Any(field => field is null) || fields.Select(field => field.Id).Distinct(StringComparer.Ordinal).Count() != fields.Count)
            throw new ArgumentException("Fill requires 1..16 field mappings with distinct ids.");
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128) throw new ArgumentException("A fill request id of 1..128 characters is required.");
        if (settleMs is < 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(settleMs), "Validation settling allows 0..1000 ms per field.");
        foreach (var field in fields)
        {
            _ = new RuntimeQueryRequest(form.SessionId, form.TopLevelId, field.Selector, maxDepth: 32);
            if (field.Selector.TreeKind != TreeKinds.Visual) throw new ArgumentException("Form field selectors must use the visual tree.");
        }
        RequestId = requestId; SettleMs = settleMs; AllowSensitiveInput = allowSensitiveInput;
    }
    [JsonPropertyName("form")] public RuntimeFormInspectionRequest Form { get; }
    [JsonPropertyName("fields")] public IReadOnlyList<RuntimeFormFieldInput> Fields { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("settleMs")] public int SettleMs { get; }
    [JsonPropertyName("allowSensitiveInput")] public bool AllowSensitiveInput { get; }
}

public sealed record RuntimeFormChoice(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("target")] RuntimeTargetContext? Target,
    [property: JsonPropertyName("selected")] bool? Selected);

public sealed record RuntimeFormField(
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("labelSource")] string LabelSource,
    [property: JsonPropertyName("nodeType")] string NodeType,
    [property: JsonPropertyName("property")] string Property,
    [property: JsonPropertyName("state")] RuntimeDesiredStateValue State,
    [property: JsonPropertyName("writable")] bool Writable,
    [property: JsonPropertyName("required")] bool? Required,
    [property: JsonPropertyName("requiredSource")] string RequiredSource,
    [property: JsonPropertyName("sensitive")] bool Sensitive,
    [property: JsonPropertyName("choices")] IReadOnlyList<RuntimeFormChoice> Choices,
    [property: JsonPropertyName("choiceCount")] int? ChoiceCount,
    [property: JsonPropertyName("choicesComplete")] bool ChoicesComplete,
    [property: JsonPropertyName("validation")] RuntimeValidationState Validation);

public sealed record RuntimeFormInspectionResponse(
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("topLevelId")] string TopLevelId,
    [property: JsonPropertyName("scopeTarget")] RuntimeTargetContext ScopeTarget,
    [property: JsonPropertyName("fields")] IReadOnlyList<RuntimeFormField> Fields,
    [property: JsonPropertyName("coverage")] RuntimeQueryCoverage Coverage,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt);

public sealed record RuntimeFormFieldResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("target")] RuntimeTargetContext? Target,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("verified")] bool Verified,
    [property: JsonPropertyName("execution")] RuntimeDesiredStateResponse? Execution,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics);

public sealed record RuntimeFormFillResponse(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("before")] RuntimeFormInspectionResponse? Before,
    [property: JsonPropertyName("after")] RuntimeFormInspectionResponse? After,
    [property: JsonPropertyName("fields")] IReadOnlyList<RuntimeFormFieldResult> Fields,
    [property: JsonPropertyName("appearedFields")] IReadOnlyList<string> AppearedFields,
    [property: JsonPropertyName("disappearedFields")] IReadOnlyList<string> DisappearedFields,
    [property: JsonPropertyName("changedFields")] IReadOnlyList<string> ChangedFields,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("replayed")] bool Replayed = false,
    [property: JsonPropertyName("submitted")] bool Submitted = false,
    [property: JsonPropertyName("rolledBack")] bool RolledBack = false);
