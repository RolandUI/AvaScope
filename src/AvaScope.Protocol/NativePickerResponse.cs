using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record NativePickerResponse
{
    [JsonConstructor]
    public NativePickerResponse(
        SessionId sessionId,
        int processId,
        string operation,
        string status,
        bool dialogDetected,
        string? selectedPath = null,
        string? message = null,
        string? correlationId = null,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? consumedAt = null,
        bool pathRedacted = false)
    {
        SessionId = sessionId;
        ProcessId = processId;
        Operation = operation;
        Status = status;
        DialogDetected = dialogDetected;
        SelectedPath = string.IsNullOrWhiteSpace(selectedPath) ? null : selectedPath;
        Message = string.IsNullOrWhiteSpace(message) ? null : message;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        ExpiresAt = expiresAt;
        ConsumedAt = consumedAt;
        PathRedacted = pathRedacted;
    }

    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("processId")] public int ProcessId { get; }
    [JsonPropertyName("operation")] public string Operation { get; }
    [JsonPropertyName("status")] public string Status { get; }
    [JsonPropertyName("dialogDetected")] public bool DialogDetected { get; }
    [JsonPropertyName("selectedPath")] public string? SelectedPath { get; }
    [JsonPropertyName("message")] public string? Message { get; }
    [JsonPropertyName("correlationId")] public string? CorrelationId { get; }
    [JsonPropertyName("expiresAt")] public DateTimeOffset? ExpiresAt { get; }
    [JsonPropertyName("consumedAt")] public DateTimeOffset? ConsumedAt { get; }
    [JsonPropertyName("pathRedacted")] public bool PathRedacted { get; }
}

public static class NativePickerOperations
{
    public const string Detect = "detect";
    public const string SelectPath = "select_path";
    public const string Confirm = "confirm";
    public const string Cancel = "cancel";
    public const string PredefineResult = "predefine_result";
    public const string ConsumePredefinedResult = "consume_predefined_result";
}

public static class NativePickerResultStates
{
    public const string Success = "success";
    public const string Cancelled = "cancelled";
    public const string UnavailablePath = "unavailable_path";
    public const string DeletedPath = "deleted_path";
    public const string Expired = "expired";
    public const string NotPrepared = "not_prepared";
}
