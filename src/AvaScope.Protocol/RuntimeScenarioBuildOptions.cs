using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeScenarioBuildOptions
{
    [JsonConstructor]
    public RuntimeScenarioBuildOptions(
        string projectPath,
        string configuration = "Debug",
        string? framework = null,
        string? runtimeIdentifier = null,
        bool noRestore = false,
        IReadOnlyList<string>? arguments = null,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null,
        int timeoutMs = 120000)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ArgumentException("Scenario build project path cannot be empty.", nameof(projectPath));
        }

        if (string.IsNullOrWhiteSpace(configuration))
        {
            throw new ArgumentException("Scenario build configuration cannot be empty.", nameof(configuration));
        }

        if (timeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), timeoutMs, "Build timeout must be positive.");
        }

        ProjectPath = Path.GetFullPath(projectPath);
        Configuration = configuration.Trim();
        Framework = string.IsNullOrWhiteSpace(framework) ? null : framework.Trim();
        RuntimeIdentifier = string.IsNullOrWhiteSpace(runtimeIdentifier) ? null : runtimeIdentifier.Trim();
        NoRestore = noRestore;
        Arguments = arguments ?? [];
        WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Path.GetDirectoryName(ProjectPath)
            : Path.GetFullPath(workingDirectory);
        Environment = environment ?? new Dictionary<string, string>();
        TimeoutMs = timeoutMs;
    }

    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; }

    [JsonPropertyName("configuration")]
    public string Configuration { get; }

    [JsonPropertyName("framework")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Framework { get; }

    [JsonPropertyName("runtimeIdentifier")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RuntimeIdentifier { get; }

    [JsonPropertyName("noRestore")]
    public bool NoRestore { get; }

    [JsonPropertyName("arguments")]
    public IReadOnlyList<string> Arguments { get; }

    [JsonPropertyName("workingDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkingDirectory { get; }

    [JsonPropertyName("environment")]
    public IReadOnlyDictionary<string, string> Environment { get; }

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; }
}

public sealed record RuntimeScenarioBuildResult
{
    [JsonConstructor]
    public RuntimeScenarioBuildResult(
        string status,
        string projectPath,
        string configuration,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        string stdoutPath,
        string stderrPath,
        int? exitCode = null,
        ProtocolError? diagnostic = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Build status cannot be empty.", nameof(status));
        }

        Status = status.Trim();
        ProjectPath = Path.GetFullPath(projectPath);
        Configuration = configuration;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        StdoutPath = Path.GetFullPath(stdoutPath);
        StderrPath = Path.GetFullPath(stderrPath);
        ExitCode = exitCode;
        Diagnostic = diagnostic;
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; }

    [JsonPropertyName("configuration")]
    public string Configuration { get; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; }

    [JsonPropertyName("stdoutPath")]
    public string StdoutPath { get; }

    [JsonPropertyName("stderrPath")]
    public string StderrPath { get; }

    [JsonPropertyName("exitCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExitCode { get; }

    [JsonPropertyName("diagnostic")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProtocolError? Diagnostic { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }
}

public static class RuntimeScenarioLifecycleStatuses
{
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string TimedOut = "timed_out";
    public const string Cancelled = "cancelled";
    public const string ProcessExited = "process_exited";
    public const string Ready = "ready";
    public const string NotRequested = "not_requested";
}

public static class RuntimeScenarioFailureStages
{
    public const string Validation = "validation";
    public const string Build = "build";
    public const string Launch = "launch";
    public const string BridgeReadiness = "bridge_readiness";
    public const string Attach = "attach";
    public const string TopLevels = "top_levels";
    public const string Workflow = "workflow";
    public const string Cleanup = "cleanup";
}
