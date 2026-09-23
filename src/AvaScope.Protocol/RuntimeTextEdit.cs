using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeTextEditRequest
{
    public const int MaximumTextLength = 8192;
    public const int MaximumReplacementLength = 4096;

    [JsonConstructor]
    public RuntimeTextEditRequest(RuntimeTargetContext target, string action = "read", string? requestId = null,
        string? expectedRevision = null, int? start = null, int? end = null, string? text = null, RuntimeEvidencePolicy? policy = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (target.TreeKind != TreeKinds.Visual || string.IsNullOrEmpty(target.NodeId)
            || string.IsNullOrEmpty(target.NodeGeneration) || string.IsNullOrEmpty(target.TopLevelGeneration))
            throw new ArgumentException("Text editing requires a fresh visual node target with both generation tokens.");
        if (action is not ("read" or "select_range" or "replace_range" or "replace_selection" or "insert"))
            throw new ArgumentException("Text action must be read, select_range, replace_range, replace_selection or insert.");
        if (action == "read")
        {
            if (requestId is not null || expectedRevision is not null || start is not null || end is not null || text is not null)
                throw new ArgumentException("Read accepts only target and optional policy.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128)
                throw new ArgumentException("A request id of 1..128 characters is required for edits and selection.");
            if (expectedRevision is null || expectedRevision.Length != 64 || expectedRevision.Any(c => !char.IsAsciiHexDigit(c)))
                throw new ArgumentException("Use the exact state revision from a fresh text read.");
            if (action is "select_range" or "replace_range")
            {
                if (start is null || end is null || start < 0 || end < start || end > MaximumTextLength)
                    throw new ArgumentException("Ranges use start-inclusive/end-exclusive UTF-16 offsets within 0..8192.");
            }
            else if (action == "insert")
            {
                if (start is null or < 0 or > MaximumTextLength || end is not null)
                    throw new ArgumentException("Insert requires a start offset within 0..8192 and no end offset.");
            }
            else if (start is not null || end is not null)
                throw new ArgumentException("Replace-selection uses the selection in the expected state, not explicit offsets.");
            if (action == "select_range" ? text is not null : text is null || text.Length > MaximumReplacementLength || !IsWellFormedUtf16(text))
                throw new ArgumentException("Selection accepts no text; edits require well-formed UTF-16 text of at most 4096 code units.");
        }
        Action = action; RequestId = requestId; ExpectedRevision = expectedRevision;
        Start = start; End = end; Text = text; Policy = policy;
    }

    public static bool IsWellFormedUtf16(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i])) { if (++i == text.Length || !char.IsLowSurrogate(text[i])) return false; }
            else if (char.IsLowSurrogate(text[i])) return false;
        }
        return true;
    }

    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("action")] public string Action { get; }
    [JsonPropertyName("requestId")] public string? RequestId { get; }
    [JsonPropertyName("expectedRevision")] public string? ExpectedRevision { get; }
    [JsonPropertyName("start")] public int? Start { get; }
    [JsonPropertyName("end")] public int? End { get; }
    [JsonPropertyName("text")] public string? Text { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeTextState(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("caret")] int Caret,
    [property: JsonPropertyName("selectionStart")] int SelectionStart,
    [property: JsonPropertyName("selectionEnd")] int SelectionEnd,
    [property: JsonPropertyName("revision")] string Revision,
    [property: JsonPropertyName("isReadOnly")] bool IsReadOnly,
    [property: JsonPropertyName("hasValidationErrors")] bool HasValidationErrors,
    [property: JsonPropertyName("acceptsReturn")] bool AcceptsReturn,
    [property: JsonPropertyName("maxLength")] int MaxLength,
    [property: JsonPropertyName("newLine")] string NewLine);

public sealed record RuntimeTextEditResponse(
    [property: JsonPropertyName("requestId")] string? RequestId,
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("verified")] bool Verified,
    [property: JsonPropertyName("before")] RuntimeTextState? Before,
    [property: JsonPropertyName("after")] RuntimeTextState? After,
    [property: JsonPropertyName("dispatchedOperations")] int DispatchedOperations,
    [property: JsonPropertyName("preparationPerformed")] bool PreparationPerformed,
    [property: JsonPropertyName("provenance")] RuntimeOperationProvenance Provenance,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("replayed")] bool Replayed = false);
