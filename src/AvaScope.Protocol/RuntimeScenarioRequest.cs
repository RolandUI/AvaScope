using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeScenarioRequest
{
    [JsonConstructor]
    public RuntimeScenarioRequest(
        IReadOnlyList<SemanticWorkflowStep> steps,
        string? requestId = null,
        RuntimeScenarioLaunchOptions? launch = null,
        RuntimeScenarioAttachOptions? attach = null,
        SessionId? sessionId = null,
        string? topLevelId = null,
        string? outputDirectory = null,
        bool captureAfterEachStep = false,
        bool allowDestructive = false,
        bool isolateState = true,
        string? isolatedStateDirectory = null,
        string? timelinePath = null,
        int maxDepth = 16,
        RuntimeScenarioPickerResult? pickerResult = null)
    {
        if (steps is null || steps.Count == 0)
        {
            throw new ArgumentException("Scenario requires at least one workflow step.", nameof(steps));
        }

        if (launch is not null && attach is not null)
        {
            throw new ArgumentException("Scenario cannot specify both launch and attach options.", nameof(attach));
        }

        if (launch is null && attach is null && sessionId is null)
        {
            throw new ArgumentException("Scenario requires launch, attach, or sessionId.", nameof(sessionId));
        }

        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Max depth cannot be negative.");
        }

        RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId.Trim();
        Launch = launch;
        Attach = attach;
        SessionId = sessionId;
        TopLevelId = string.IsNullOrWhiteSpace(topLevelId) ? null : topLevelId.Trim();
        Steps = steps;
        OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? null : Path.GetFullPath(outputDirectory);
        CaptureAfterEachStep = captureAfterEachStep;
        AllowDestructive = allowDestructive;
        IsolateState = isolateState;
        IsolatedStateDirectory = string.IsNullOrWhiteSpace(isolatedStateDirectory) ? null : Path.GetFullPath(isolatedStateDirectory);
        TimelinePath = string.IsNullOrWhiteSpace(timelinePath) ? null : Path.GetFullPath(timelinePath);
        MaxDepth = maxDepth;
        PickerResult = pickerResult;
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("launch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeScenarioLaunchOptions? Launch { get; }

    [JsonPropertyName("attach")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeScenarioAttachOptions? Attach { get; }

    [JsonPropertyName("sessionId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SessionId? SessionId { get; }

    [JsonPropertyName("topLevelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelId { get; }

    [JsonPropertyName("steps")]
    public IReadOnlyList<SemanticWorkflowStep> Steps { get; }

    [JsonPropertyName("outputDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputDirectory { get; }

    [JsonPropertyName("captureAfterEachStep")]
    public bool CaptureAfterEachStep { get; }

    [JsonPropertyName("allowDestructive")]
    public bool AllowDestructive { get; }

    [JsonPropertyName("isolateState")]
    public bool IsolateState { get; }

    [JsonPropertyName("isolatedStateDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IsolatedStateDirectory { get; }

    [JsonPropertyName("timelinePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimelinePath { get; }

    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; }

    [JsonPropertyName("pickerResult")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeScenarioPickerResult? PickerResult { get; }
}

public sealed record RuntimeScenarioPickerResult
{
    [JsonConstructor]
    public RuntimeScenarioPickerResult(
        string result,
        string? path = null,
        string? correlationId = null,
        int ttlMs = 30000)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new ArgumentException("Picker result cannot be empty.", nameof(result));
        }

        if (ttlMs is < 100 or > 300000)
        {
            throw new ArgumentOutOfRangeException(nameof(ttlMs), ttlMs, "Picker result TTL must be between 100 and 300000 ms.");
        }

        Result = result.Trim();
        Path = string.IsNullOrWhiteSpace(path) ? null : path;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        TtlMs = ttlMs;
    }

    [JsonPropertyName("result")]
    public string Result { get; }

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; }

    [JsonPropertyName("correlationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CorrelationId { get; }

    [JsonPropertyName("ttlMs")]
    public int TtlMs { get; }
}

public sealed record RuntimeScenarioLaunchOptions
{
    [JsonConstructor]
    public RuntimeScenarioLaunchOptions(
        string command,
        string? arguments = null,
        string? workingDirectory = null,
        string? displayName = null,
        string? manifestDirectory = null,
        string? outputDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null,
        int timeoutMs = 15000)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Scenario launch command cannot be empty.", nameof(command));
        }

        if (timeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), timeoutMs, "Launch timeout must be positive.");
        }

        Command = command.Trim();
        Arguments = string.IsNullOrWhiteSpace(arguments) ? null : arguments;
        WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? null : Path.GetFullPath(workingDirectory);
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        ManifestDirectory = string.IsNullOrWhiteSpace(manifestDirectory) ? null : Path.GetFullPath(manifestDirectory);
        OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? null : Path.GetFullPath(outputDirectory);
        Environment = environment ?? new Dictionary<string, string>();
        TimeoutMs = timeoutMs;
    }

    [JsonPropertyName("command")]
    public string Command { get; }

    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Arguments { get; }

    [JsonPropertyName("workingDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkingDirectory { get; }

    [JsonPropertyName("displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; }

    [JsonPropertyName("manifestDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ManifestDirectory { get; }

    [JsonPropertyName("outputDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputDirectory { get; }

    [JsonPropertyName("environment")]
    public IReadOnlyDictionary<string, string> Environment { get; }

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; }
}

public sealed record RuntimeScenarioAttachOptions
{
    [JsonConstructor]
    public RuntimeScenarioAttachOptions(
        int? processId = null,
        SessionId? sessionId = null,
        string? processName = null,
        string? manifestPath = null,
        bool latest = false)
    {
        if (processId is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be positive.");
        }

        if (!latest
            && processId is null
            && sessionId is null
            && string.IsNullOrWhiteSpace(processName)
            && string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new ArgumentException("Scenario attach requires latest, processId, processName, sessionId, or manifestPath.", nameof(latest));
        }

        ProcessId = processId;
        SessionId = sessionId;
        ProcessName = string.IsNullOrWhiteSpace(processName) ? null : processName.Trim();
        ManifestPath = string.IsNullOrWhiteSpace(manifestPath) ? null : Path.GetFullPath(manifestPath);
        Latest = latest;
    }

    [JsonPropertyName("processId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ProcessId { get; }

    [JsonPropertyName("sessionId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SessionId? SessionId { get; }

    [JsonPropertyName("processName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcessName { get; }

    [JsonPropertyName("manifestPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ManifestPath { get; }

    [JsonPropertyName("latest")]
    public bool Latest { get; }
}
