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
        RuntimeScenarioPickerResult? pickerResult = null,
        IReadOnlyList<SemanticWorkflowTopLevelAlias>? topLevelAliases = null,
        IReadOnlyDictionary<string, string>? variables = null,
        IReadOnlyList<SemanticWorkflowFragment>? fragments = null,
        int workflowTimeoutMs = SemanticWorkflowLimits.DefaultWorkflowTimeoutMs,
        SemanticWorkflowEvidenceOptions? evidence = null,
        RuntimeScenarioBuildOptions? build = null,
        bool terminateLaunchedProcess = false)
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

        if (workflowTimeoutMs is < 1 or > SemanticWorkflowLimits.MaximumWorkflowTimeoutMs)
        {
            throw new ArgumentOutOfRangeException(nameof(workflowTimeoutMs), workflowTimeoutMs, $"Workflow timeout must be between 1 and {SemanticWorkflowLimits.MaximumWorkflowTimeoutMs} ms.");
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
        TopLevelAliases = topLevelAliases ?? Array.Empty<SemanticWorkflowTopLevelAlias>();
        Variables = variables ?? new Dictionary<string, string>();
        Fragments = fragments ?? Array.Empty<SemanticWorkflowFragment>();
        WorkflowTimeoutMs = workflowTimeoutMs;
        Evidence = evidence;
        Build = build;
        TerminateLaunchedProcess = terminateLaunchedProcess;
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

    [JsonPropertyName("topLevelAliases")]
    public IReadOnlyList<SemanticWorkflowTopLevelAlias> TopLevelAliases { get; }

    [JsonPropertyName("variables")]
    public IReadOnlyDictionary<string, string> Variables { get; }

    [JsonPropertyName("fragments")]
    public IReadOnlyList<SemanticWorkflowFragment> Fragments { get; }

    [JsonPropertyName("workflowTimeoutMs")]
    public int WorkflowTimeoutMs { get; }

    [JsonPropertyName("evidence")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SemanticWorkflowEvidenceOptions? Evidence { get; }

    [JsonPropertyName("build")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeScenarioBuildOptions? Build { get; }

    [JsonPropertyName("terminateLaunchedProcess")]
    public bool TerminateLaunchedProcess { get; }
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
        string? command = null,
        string? arguments = null,
        string? workingDirectory = null,
        string? displayName = null,
        string? manifestDirectory = null,
        string? outputDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null,
        int timeoutMs = 15000,
        string? projectPath = null,
        IReadOnlyList<string>? argumentList = null,
        string configuration = "Debug",
        string? framework = null,
        bool noBuild = false)
    {
        if (string.IsNullOrWhiteSpace(command) == string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ArgumentException("Scenario launch requires exactly one command or projectPath.", nameof(command));
        }

        if (!string.IsNullOrWhiteSpace(arguments) && argumentList is { Count: > 0 })
        {
            throw new ArgumentException("Scenario launch cannot specify both arguments and argumentList.", nameof(argumentList));
        }

        if (!string.IsNullOrWhiteSpace(projectPath) && !string.IsNullOrWhiteSpace(arguments))
        {
            throw new ArgumentException("Project launch arguments must use argumentList so values remain tokenized and redacted.", nameof(arguments));
        }

        if (string.IsNullOrWhiteSpace(configuration))
        {
            throw new ArgumentException("Scenario launch configuration cannot be empty.", nameof(configuration));
        }

        if (timeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), timeoutMs, "Launch timeout must be positive.");
        }

        Command = string.IsNullOrWhiteSpace(command) ? null : command.Trim();
        ProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetFullPath(projectPath);
        Arguments = string.IsNullOrWhiteSpace(arguments) ? null : arguments;
        ArgumentList = argumentList ?? [];
        WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? null : Path.GetFullPath(workingDirectory);
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        ManifestDirectory = string.IsNullOrWhiteSpace(manifestDirectory) ? null : Path.GetFullPath(manifestDirectory);
        OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? null : Path.GetFullPath(outputDirectory);
        Environment = environment ?? new Dictionary<string, string>();
        TimeoutMs = timeoutMs;
        Configuration = configuration.Trim();
        Framework = string.IsNullOrWhiteSpace(framework) ? null : framework.Trim();
        NoBuild = noBuild;
    }

    [JsonPropertyName("command")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Command { get; }

    [JsonPropertyName("projectPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectPath { get; }

    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Arguments { get; }

    [JsonPropertyName("argumentList")]
    public IReadOnlyList<string> ArgumentList { get; }

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

    [JsonPropertyName("configuration")]
    public string Configuration { get; }

    [JsonPropertyName("framework")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Framework { get; }

    [JsonPropertyName("noBuild")]
    public bool NoBuild { get; }
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
