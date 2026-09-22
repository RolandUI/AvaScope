using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AvaScope.Protocol;

namespace AvaScope.Core;

/// <summary>Versioned configuration that resolves into the existing scenario engine.</summary>
public static class AgentTestProfiles
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static CoreResult<AgentTestProfileResponse> Resolve(string profileFile, string profileName, string? platform = null)
    {
        var resolved = Read(profileFile, profileName, platform ?? CurrentPlatform(), "{runId}");
        return resolved.Success ? CoreResult<AgentTestProfileResponse>.Ok(resolved.Value!.Response)
            : CoreResult<AgentTestProfileResponse>.Fail(resolved.Error!);
    }

    public static async Task<CoreResult<RuntimeScenarioResponse>> RunAsync(LocalBridgeClient bridgeClient,
        string profileFile, string profileName, CancellationToken cancellationToken = default)
    {
        var resolved = Read(profileFile, profileName, CurrentPlatform(), Guid.NewGuid().ToString("N"));
        if (!resolved.Success) return CoreResult<RuntimeScenarioResponse>.Fail(resolved.Error!);
        return await new RuntimeScenarioRunner().RunAsync(bridgeClient, resolved.Value!.Request, cancellationToken);
    }

    private static CoreResult<ResolvedProfile> Read(string profileFile, string profileName, string platform, string runId)
    {
        var secrets = new List<string>();
        try
        {
            if (string.IsNullOrWhiteSpace(profileFile) || !Path.IsPathFullyQualified(profileFile) || string.IsNullOrWhiteSpace(profileName))
                throw new ArgumentException("Select an absolute profileFile and a non-empty profileName.");
            if (profileName.Length > 64 || profileName is "." or ".." || profileName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-' or '.')))
                throw new ArgumentException("Profile names must use 1–64 ASCII letters, digits, periods, underscores or hyphens and cannot be '.' or '..'.");
            if (platform is not ("windows" or "linux" or "macos")) throw new ArgumentException("Supported profile platforms are windows, linux and macos.");
            var path = Path.GetFullPath(profileFile);
            if (new FileInfo(path).Length > 1024 * 1024) throw new ArgumentException("Test profile exceeds 1 MiB.");
            var bytes = File.ReadAllBytes(path);
            var document = JsonNode.Parse(bytes) as JsonObject ?? throw new JsonException("Test profile must be a JSON object.");
            RequireFields(document, "schemaVersion", "profiles");
            if (document["schemaVersion"]?.GetValue<int>() != 1)
                return CoreResult<ResolvedProfile>.Fail(new("test_profile_version_unsupported", "Only agent test profile schemaVersion 1 is supported."));
            var profiles = document["profiles"] as JsonObject ?? throw new JsonException("profiles must be a named object.");
            if (profiles.Count is < 1 or > 64) throw new ArgumentException("Test profile files must contain 1–64 named profiles.");
            var profile = profiles[profileName]?.DeepClone() as JsonObject ?? throw new ArgumentException("The selected named profile does not exist.");
            RequireFields(profile, "scenario", "provider", "environmentReferences", "platforms");
            if (profile["platforms"] is JsonObject overrides)
            {
                RequireFields(overrides, "windows", "linux", "macos");
                foreach (var item in overrides)
                    RequireFields(item.Value as JsonObject ?? throw new JsonException("A platform override must be an object."), "scenario", "provider", "environmentReferences");
                if (overrides[platform] is JsonObject selected) Merge(profile, selected);
            }
            else if (profile["platforms"] is not null) throw new JsonException("platforms must be an object.");
            profile.Remove("platforms");
            var directory = Path.GetDirectoryName(path)!;
            ExpandTokens(profile, directory, runId);
            var scenario = profile["scenario"] as JsonObject ?? throw new JsonException("The profile requires a scenario object.");
            var launch = scenario["launch"] as JsonObject;
            var build = scenario["build"] as JsonObject;
            if (launch is null || scenario["attach"] is not null || scenario["sessionId"] is not null)
                throw new ArgumentException("Agent test profiles require an explicitly owned launch; use an explicit scenario request to attach an existing session.");
            foreach (var config in new[] { launch, build }.Where(item => item is not null))
                if (config!["environment"] is not null) throw new ArgumentException("Use environmentReferences instead of checked-in environment values.");

            ResolvePaths(scenario, directory, "outputDirectory", "isolatedStateDirectory", "timelinePath");
            ResolvePaths(launch, directory, "projectPath", "workingDirectory", "manifestDirectory", "outputDirectory");
            if (launch["command"] is JsonValue commandValue && commandValue.GetValue<string>() is var command && command.IndexOfAny(['/', '\\']) >= 0)
                launch["command"] = Path.GetFullPath(command, directory);
            if (build is not null) ResolvePaths(build, directory, "projectPath", "workingDirectory");
            scenario["requestId"] ??= $"{profileName}-{runId}";
            scenario["outputDirectory"] ??= Path.Combine(directory, "artifacts", "avascope", profileName, runId);
            if (!scenario["outputDirectory"]!.GetValue<string>().Contains(runId, StringComparison.Ordinal))
                scenario["outputDirectory"] = Path.Combine(scenario["outputDirectory"]!.GetValue<string>(), runId);
            scenario["terminateLaunchedProcess"] ??= true;
            var output = scenario["outputDirectory"]!.GetValue<string>();
            launch["manifestDirectory"] ??= Path.Combine(output, "sessions");
            var environmentNames = new List<string>();
            var references = profile["environmentReferences"] as JsonObject;
            if (profile["environmentReferences"] is not null && references is null) throw new JsonException("environmentReferences must be an object.");
            if (references is not null)
            {
                RequireFields(references, "launch", "build");
                ApplyEnvironmentReferences(references["launch"], launch, secrets, environmentNames);
                if (references["build"] is not null && build is null) throw new ArgumentException("Build environment references require an explicit build section.");
                if (build is not null) ApplyEnvironmentReferences(references["build"], build, secrets, environmentNames);
            }

            ProviderVerificationResponse? providerResponse = null;
            if (profile["provider"] is JsonObject provider)
            {
                RequireFields(provider, "directory", "version", "manifestSha256");
                ResolvePaths(provider, directory, "directory");
                var providerPath = provider["directory"]?.GetValue<string>() ?? throw new ArgumentException("Provider directory is required.");
                var verified = ProviderVerifier.Verify(providerPath, provider["version"]?.GetValue<string>(), provider["manifestSha256"]?.GetValue<string>());
                if (!verified.Success) return CoreResult<ResolvedProfile>.Fail(verified.Error!);
                providerResponse = verified.Value!;
                var environment = launch["environment"] as JsonObject ?? new JsonObject();
                if (launch["environment"] is null) launch["environment"] = environment;
                foreach (var name in new[] { "UI_INSPECTION_PROVIDER_PATH", "UI_INSPECTION_PROVIDER_VERSION", "UI_INSPECTION_PROVIDER_SHA256" })
                    if (environment.ContainsKey(name)) throw new ArgumentException("Provider identity cannot also be supplied by an environment reference.");
                environment["UI_INSPECTION_PROVIDER_PATH"] = providerResponse.Directory;
                environment["UI_INSPECTION_PROVIDER_VERSION"] = providerResponse.ProviderVersion;
                environment["UI_INSPECTION_PROVIDER_SHA256"] = providerResponse.ManifestSha256;
            }
            else if (profile["provider"] is not null) throw new JsonException("provider must be an object.");

            // Reuse existing evidence policy so referenced secrets are redacted before logs/artifacts are persisted.
            var evidence = scenario["evidence"] as JsonObject ?? new JsonObject();
            if (scenario["evidence"] is null) scenario["evidence"] = evidence;
            else if (scenario["evidence"] is not JsonObject) throw new JsonException("evidence must be an object.");
            ResolvePaths(evidence, directory, "reportDirectory");
            var policy = evidence["policy"] as JsonObject ?? new JsonObject();
            if (evidence["policy"] is null) evidence["policy"] = policy;
            else if (evidence["policy"] is not JsonObject) throw new JsonException("policy must be an object.");
            ResolvePaths(policy, directory, "ownedEvidenceRoot");
            policy["ownedEvidenceRoot"] ??= Path.GetDirectoryName(output)!;
            var redactions = policy["redactedText"] as JsonArray ?? new JsonArray();
            if (policy["redactedText"] is null) policy["redactedText"] = redactions;
            foreach (var secret in secrets.Distinct(StringComparer.Ordinal)) redactions.Add(secret);
            // A profile is an explicit scenario recipe; do not narrow its already supported action set by adding redaction.
            policy["allowedActions"] ??= JsonSerializer.SerializeToNode(SemanticWorkflowActions.All, JsonOptions);
            ResolveStepPaths(scenario, directory);
            var request = scenario.Deserialize<RuntimeScenarioRequest>(JsonOptions) ?? throw new JsonException("Invalid scenario request.");
            var plan = SemanticWorkflowCompiler.Compile(new SemanticWorkflowRequest(new SessionId("profile-validation"), "topLevel:profile-validation",
                request.Steps, outputDirectory: output, topLevelAliases: request.TopLevelAliases, variables: request.Variables, fragments: request.Fragments,
                validateOnly: true, timeoutMs: request.WorkflowTimeoutMs, evidence: request.Evidence));
            if (!plan.Plan.Valid) throw new ArgumentException("The profile workflow is invalid: " + string.Join("; ", plan.Plan.Diagnostics.Select(item => item.Message)));
            var redactor = new RuntimeEvidencePolicyEnforcer(request.Evidence!.Policy!);
            var safeJson = redactor.SanitizeJson(request);
            if (!safeJson.Success) return CoreResult<ResolvedProfile>.Fail(safeJson.Error!);
            using var safeDocument = JsonDocument.Parse(safeJson.Value!);
            return CoreResult<ResolvedProfile>.Ok(new(request, new(1, path, profileName, platform,
                Convert.ToHexStringLower(SHA256.HashData(bytes)), safeDocument.RootElement.Clone(), providerResponse,
                environmentNames.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                ["Precedence: base profile, then selected platform override; arrays replace, objects merge, null removes a value.",
                 "Paths resolve relative to the profile file; {profileDir} and {runId} are the only substitutions. The read-only view preserves {runId}; execution allocates a unique value.",
                 "Environment references default to secret; literal environment dictionaries are rejected. Provider verification never activates the bridge."])));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException or NotSupportedException)
        {
            var message = exception.Message;
            foreach (var secret in secrets.OrderByDescending(value => value.Length)) message = message.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
            return CoreResult<ResolvedProfile>.Fail(new("test_profile_invalid", message));
        }
    }

    private static void ApplyEnvironmentReferences(JsonNode? references, JsonObject target, List<string> secrets, List<string> names)
    {
        if (references is null) return;
        if (references is not JsonObject values || values.Count > 64) throw new ArgumentException("Each environment reference section must contain at most 64 entries.");
        var environment = new JsonObject();
        target["environment"] = environment;
        foreach (var (targetName, node) in values)
        {
            if (targetName.Length is < 1 or > 128 || targetName.Contains('=') || targetName.Contains('\0')) throw new ArgumentException("Invalid environment variable name.");
            var reference = node as JsonObject ?? throw new JsonException("Environment references require {name, secret?, required?} objects.");
            RequireFields(reference, "name", "secret", "required");
            var name = reference["name"]?.GetValue<string>() ?? throw new ArgumentException("Environment reference name is required.");
            var value = Environment.GetEnvironmentVariable(name);
            if (value is null)
            {
                if (reference["required"]?.GetValue<bool>() != false) throw new ArgumentException($"Required environment reference '{name}' is unavailable.");
                continue;
            }
            if (value.Length > 32768) throw new ArgumentException("Environment reference exceeds 32768 characters.");
            names.Add(name);
            if (reference["secret"]?.GetValue<bool>() != false && !string.IsNullOrEmpty(value)) secrets.Add(value);
            environment[targetName] = value;
        }
    }

    private static void RequireFields(JsonObject value, params string[] allowed)
    {
        foreach (var field in value)
            if (!allowed.Contains(field.Key, StringComparer.Ordinal)) throw new JsonException($"Unknown profile field '{field.Key}'.");
    }

    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            if (value is null) target.Remove(key);
            else if (value is JsonObject sourceObject && target[key] is JsonObject targetObject) Merge(targetObject, sourceObject);
            else target[key] = value.DeepClone();
        }
    }

    private static void ExpandTokens(JsonNode node, string directory, string runId)
    {
        if (node is JsonObject obj)
        {
            foreach (var (key, value) in obj.ToArray())
            {
                if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text)) obj[key] = text.Replace("{profileDir}", directory, StringComparison.Ordinal).Replace("{runId}", runId, StringComparison.Ordinal);
                else if (value is not null) ExpandTokens(value, directory, runId);
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                if (array[index] is JsonValue scalar && scalar.TryGetValue<string>(out var text)) array[index] = text.Replace("{profileDir}", directory, StringComparison.Ordinal).Replace("{runId}", runId, StringComparison.Ordinal);
                else if (array[index] is { } value) ExpandTokens(value, directory, runId);
            }
        }
    }

    private static void ResolvePaths(JsonObject value, string directory, params string[] names)
    {
        foreach (var name in names)
            if (value[name] is JsonValue path) value[name] = Path.GetFullPath(path.GetValue<string>(), directory);
    }

    private static void ResolveStepPaths(JsonNode node, string directory)
    {
        if (node is JsonObject obj)
        {
            ResolvePaths(obj, directory, "screenshotPath");
            foreach (var value in obj.Select(item => item.Value).Where(item => item is JsonObject or JsonArray).ToArray()) ResolveStepPaths(value!, directory);
        }
        else if (node is JsonArray array)
            foreach (var value in array.Where(item => item is JsonObject or JsonArray)) ResolveStepPaths(value!, directory);
    }

    private static string CurrentPlatform() => OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsLinux() ? "linux" : "unsupported";
    private sealed record ResolvedProfile(RuntimeScenarioRequest Request, AgentTestProfileResponse Response);
}
