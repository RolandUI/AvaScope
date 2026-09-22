using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AvaScope.Protocol;

namespace AvaScope.Core;

/// <summary>Exports AvaScope execution evidence; never imports another framework or infers successful assertions.</summary>
public static class WorkflowExporter
{
    private const int MaximumBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly Regex Tokens = new(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_.-]*)\}", RegexOptions.CultureInvariant);

    public static CoreResult<WorkflowExportResponse> Export(WorkflowExportRequest request)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            var source = request.Source;
            var recording = request.Recording;
            if (source is null || recording is null || recording.RequestId != source.RequestId || recording.SessionId != source.SessionId)
                return Fail<WorkflowExportResponse>("workflow_export_recording_mismatch", "Supply the original AvaScope workflow request and its corresponding complete execution response.");
            if (JsonSerializer.SerializeToUtf8Bytes(request).Length > MaximumBytes || recording.Steps.Count > SemanticWorkflowLimits.MaximumEstimatedExecutions)
                return Fail<WorkflowExportResponse>("workflow_export_limit", "Export input is limited to 1 MiB and the existing workflow execution limits.");
            if (recording.ResponseBudget?.Truncated == true)
                return Fail<WorkflowExportResponse>("workflow_export_incomplete", "Use the complete recording artifact; a truncated response cannot establish which steps ran.");
            if (recording.Steps.Count == 0 || !recording.Metadata.TryGetValue("requestDefinitionSha256", out var sourceHash)
                || sourceHash != SemanticWorkflowRunner.DefinitionHash(source))
                return Fail<WorkflowExportResponse>("workflow_export_definition_mismatch", "Use an executed recording with the matching requestDefinitionSha256; validate-only or changed source definitions are not execution evidence.");
            var original = SemanticWorkflowCompiler.Compile(source);
            if (!original.Plan.Valid) return Fail<WorkflowExportResponse>("workflow_export_invalid_source", "The original workflow must pass the existing compiler before export.");
            var compiledSteps = Flatten(original.Roots).ToArray();

            var policy = source.Evidence?.Policy is { } rules ? new RuntimeEvidencePolicyEnforcer(rules) : null;
            var workflow = JsonSerializer.SerializeToNode(source)!.AsObject();
            var reviews = new List<WorkflowExportReview>();
            var verified = new List<string>();
            var parameters = new List<WorkflowExportParameter>();
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var sourceNames = source.Variables.Keys.Concat(source.Fragments.SelectMany(fragment => fragment.Parameters)).ToHashSet(StringComparer.Ordinal);
            var stepIndex = 0;
            if (recording.Status != "passed") Review("recording_not_passed", null, "The recorded workflow did not pass; review the failure before using it as a regression.");
            foreach (var pair in request.Parameters ?? new Dictionary<string, string>())
            {
                if (!Regex.IsMatch(pair.Key, @"^[A-Za-z_][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant))
                    return Fail<WorkflowExportResponse>("workflow_export_parameter_invalid", "Parameter names must be identifiers of at most 64 characters.");
                AddValue(pair.Value, "Explicit parameter " + pair.Key, "export_" + pair.Key);
            }
            foreach (var value in source.Variables.Values) AddValue(value, "Recorded workflow variable");
            if (policy is not null)
                foreach (var value in policy.Policy.RedactedText.Concat(policy.Policy.RedactedAutomationIds).Concat(policy.Policy.ExcludedControlAutomationIds))
                    AddValue(value, "Required redaction or excluded-control value");

            var aliases = workflow["topLevelAliases"]!.AsArray();
            var defaultAlias = request.DefaultTopLevelAlias;
            if (defaultAlias is not null)
            {
                var existing = source.TopLevelAliases.FirstOrDefault(alias => alias.Alias == defaultAlias.Alias);
                if (existing is not null && JsonSerializer.Serialize(existing.Selector) != JsonSerializer.Serialize(defaultAlias.Selector))
                    return Fail<WorkflowExportResponse>("workflow_export_alias_conflict", "The default alias conflicts with the source alias definition.");
                if (existing is null) aliases.Add(JsonSerializer.SerializeToNode(defaultAlias));
            }
            foreach (var alias in aliases.OfType<JsonObject>())
            {
                var selector = alias["selector"]!.AsObject();
                if (selector["sessionId"] is not null) selector["sessionId"] = "${export_session}";
                if (string.IsNullOrWhiteSpace(selector["title"]?.GetValue<string>()))
                    Review("window_selector_requires_review", null, "A window alias has no stable title; uniqueness and intended routing require review.");
            }
            if (aliases.Count == 0) Review("stable_window_alias_required", null, "Supply a stable defaultTopLevelAlias; transient top-level ids cannot be replayed.", true);
            workflow["topLevelId"] = null;
            Walk(workflow["steps"]!.AsArray());
            foreach (var fragment in workflow["fragments"]!.AsArray().OfType<JsonObject>()) Walk(fragment["steps"]!.AsArray());
            if (verified.Count == 0) Review("no_verified_assertions", null, "No successful explicit assertion, state wait or postcondition was recorded. Add a condition before relying on this flow as a regression test.");

            // Paths and live authorization are rebound explicitly for every replay.
            workflow["sessionId"] = "${export_session}";
            workflow["requestId"] = "${export_request}";
            workflow["outputDirectory"] = "${export_output}";
            workflow["validateOnly"] = true;
            if (source.IsolatedStateDirectory is not null) workflow["isolatedStateDirectory"] = "${export_state}";
            if (workflow["evidence"] is JsonObject evidence)
            {
                evidence["reportDirectory"] = null;
                if (evidence["policy"] is JsonObject exportedPolicy)
                {
                    exportedPolicy["ownedEvidenceRoot"] = "${export_evidence_root}";
                    if (source.Evidence!.Policy!.AuthorizedSessionIds.Count > 0)
                        exportedPolicy["authorizedSessionIds"] = new JsonArray("${export_session}");
                    if (source.Evidence.Policy.AuthorizedProcessIds.Count > 0)
                        exportedPolicy["authorizedProcessIds"] = new JsonArray("${export_process}");
                }
            }
            ReplaceStrings(workflow, value => ReplaceLiterals(value, values));
            var document = new WorkflowExportDocument(1, JsonSerializer.SerializeToElement(workflow), parameters, reviews, verified,
                source.IsolatedStateDirectory is not null, policy is not null, source.Evidence?.Policy?.AuthorizedProcessIds.Count > 0);
            var rebound = aliases.Count == 0 ? Fail<SemanticWorkflowRequest>("workflow_export_alias_required", "Supply a stable window alias.")
                : Bind(document, new("unused", source.SessionId, source.OutputDirectory ?? Path.GetTempPath(), values,
                AcknowledgeReview: true, IsolatedStateDirectory: source.IsolatedStateDirectory,
                EvidenceRoot: source.Evidence?.Policy?.OwnedEvidenceRoot,
                AuthorizedProcessId: source.Evidence?.Policy?.AuthorizedProcessIds.FirstOrDefault(), AllowDestructive: source.AllowDestructive), enforceReview: false);
            var validated = rebound.Success && SemanticWorkflowCompiler.Compile(rebound.Value!).Plan.Valid;
            if (!validated) Review("export_validation_failed", null, "The rebound export does not compile; correct its selectors or composition and export again.", true);
            if (policy is not null)
            {
                var sanitized = policy.SanitizeJson(document);
                if (!sanitized.Success || !JsonNode.DeepEquals(JsonNode.Parse(sanitized.Value!), JsonSerializer.SerializeToNode(document)))
                    return Fail<WorkflowExportResponse>("workflow_export_redaction_failed", "Some policy-protected content could not be parameterized safely. No export was written.");
            }

            var exportJson = JsonSerializer.Serialize(document, JsonOptions);
            var workflowJson = workflow.ToJsonString(JsonOptions);
            if (Encoding.UTF8.GetByteCount(exportJson) > MaximumBytes || Encoding.UTF8.GetByteCount(workflowJson) > MaximumBytes)
                return Fail<WorkflowExportResponse>("workflow_export_limit", "The formatted export exceeds the 1 MiB replay limit.");
            var directory = Path.Combine(Path.GetFullPath(request.OutputDirectory), "avascope-export-" + Guid.NewGuid().ToString("N"));
            var exportPath = Path.Combine(directory, "export.json");
            var workflowPath = Path.Combine(directory, "workflow.json");
            if (policy is not null)
            {
                var prepared = policy.PrepareRun(directory, [exportPath, workflowPath], "workflow-export");
                if (!prepared.Success) return CoreResult<WorkflowExportResponse>.Fail(prepared.Error!);
            }
            else Directory.CreateDirectory(directory);
            File.WriteAllText(exportPath, exportJson);
            File.WriteAllText(workflowPath, workflowJson);
            return CoreResult<WorkflowExportResponse>.Ok(new(reviews.Count == 0 ? "ready" : "needs_review", exportPath, workflowPath, parameters, reviews, verified, validated));

            void Review(string code, string? step, string message, bool blocks = false) => reviews.Add(new(code, step, message, blocks));
            void AddValue(string value, string purpose, string? name = null)
            {
                if (string.IsNullOrEmpty(value) || values.ContainsValue(value)) return;
                if (value.Length > 8192 || values.Count >= 128) throw new ArgumentException("Export allows at most 128 parameter values, each at most 8192 characters.");
                name ??= "export_value_" + (values.Count + 1);
                if (sourceNames.Contains(name) || name is "export_session" or "export_request" or "export_output" or "export_state" or "export_evidence_root" or "export_process"
                    || !values.TryAdd(name, value)) throw new ArgumentException("An export parameter conflicts with a reserved or source variable name.");
                parameters.Add(new(name, purpose));
            }
            void Walk(JsonArray steps)
            {
                foreach (var step in steps.OfType<JsonObject>())
                {
                    var oldId = step["id"]!.GetValue<string>();
                    var action = step["action"]!.GetValue<string>();
                    var id = "step-" + (++stepIndex).ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
                    step["id"] = id;
                    var matches = recording.Steps.Where(result => result.StepId == oldId && result.Action == action).ToArray();
                    if (matches.Length == 0 || matches.Any(result => result.Status != "passed"))
                        Review("execution_not_verified", id, "This step was skipped, failed or not present in the recording; no successful assertion is claimed.");
                    if (policy is not null && !policy.AuthorizeAction(action, step["customActionName"]?.GetValue<string>()).Success)
                        Review("action_policy_denied", id, "The source action policy does not authorize this step.", true);
                    var observed = matches.Length == 1 ? matches[0] : null;
                    if (matches.Length > 1) Review("repeated_step_evidence", id, "Multiple executions share this source step id; review branch/retry evidence individually.");
                    var explicitAssertion = action == SemanticWorkflowActions.AssertState && observed?.Status == "passed";
                    var verifiedPostcondition = observed is { Status: "passed", Verification: { Status: "passed", Observation.Matched: true } }
                        && step["verify"] is not null
                        && compiledSteps.Any(compiled => compiled.Step.Id == oldId && compiled.Step.Verify is { } verify
                            && JsonNode.DeepEquals(JsonNode.Parse(policy?.SanitizeJson(verify.Condition).Value ?? JsonSerializer.Serialize(verify.Condition)),
                                JsonSerializer.SerializeToNode(observed.Verification.Condition)));
                    var verifiedWait = observed is { Status: "passed", WaitObservation.Matched: true };
                    if (explicitAssertion || verifiedPostcondition || verifiedWait) verified.Add(id);
                    if (IsControl(action) && !verifiedPostcondition)
                        Review("missing_verified_postcondition", id, "The control step has no recorded successful postcondition. Dispatch is not proof of the intended result.");
                    if (action == SemanticWorkflowActions.Wait) Review("fixed_sleep", id, "Replace this recorded fixed delay with an explicit state wait; no condition was invented.");
                    if (action is SemanticWorkflowActions.KeyDown or SemanticWorkflowActions.KeyUp or SemanticWorkflowActions.PressAndHold)
                        Review("unpaired_input", id, "A held-key/button operation is not a self-contained replay step. Use a paired action.", true);
                    if (step["inputExecution"] is JsonObject execution)
                    {
                        if (execution["destinationX"] is not null || execution["destinationY"] is not null)
                            Review("coordinate_input", id, "Recorded coordinates require review on a fresh layout; prefer a semantic destination selector.");
                        if (execution["expectedGeometryRevision"] is JsonValue geometry)
                        {
                            AddValue(geometry.GetValue<string>(), "Fresh activation geometry for " + id);
                            Review("activation_geometry_rebind", id, "Bind the geometry revision returned by explain_action for the replay session and current target layout.");
                        }
                    }
                    if (step["text"] is JsonValue literal && !Tokens.IsMatch(literal.GetValue<string>()))
                        AddValue(literal.GetValue<string>(), "Literal text at " + id);
                    if (step["topLevelAlias"] is null && defaultAlias is not null) step["topLevelAlias"] = defaultAlias.Alias;
                    if (step["topLevelAlias"] is null && action != SemanticWorkflowActions.UseFragment)
                        Review("step_window_alias_required", id, "This step needs a stable window alias.", true);
                    CheckSelector(step["selector"] as JsonObject, observed?.Inspection, id);
                    CheckSelector(step["destinationSelector"] as JsonObject, null, id);
                    if (step["verify"] is JsonObject verify) CheckSelector(verify["selector"] as JsonObject, null, id);
                    CheckCondition(step["waitCondition"] as JsonObject, id);
                    if (step["verify"] is JsonObject verification) CheckCondition(verification["condition"] as JsonObject, id);
                    if (step["screenshotPath"] is not null) step["screenshotPath"] = null;
                    foreach (var branch in new[] { "then", "else", "steps" }) if (step[branch] is JsonArray children) Walk(children);
                }
            }
            void CheckSelector(JsonObject? selector, InspectNodeResponse? inspection, string id)
            {
                if (selector is null) return;
                var transient = selector["nodeId"]?.GetValue<string>();
                if (transient is not null)
                {
                    selector["nodeId"] = null;
                    if (inspection?.NodeId == transient)
                    {
                        if (!string.IsNullOrWhiteSpace(inspection.AutomationId)) selector["automationId"] = inspection.AutomationId;
                        else if (!string.IsNullOrWhiteSpace(inspection.Name)) selector["name"] = inspection.Name;
                    }
                    Review("selector_rebound", id, "A transient node id was removed. The semantic selector must resolve uniquely on replay.");
                }
                if (new[] { "automationId", "name", "bindingPath", "commandName" }.All(key => string.IsNullOrWhiteSpace(selector[key]?.GetValue<string>())))
                    Review("unstable_selector", id, "The selector has no stable automation id, name, binding path or command. Supply one before replay.", true);
            }
            void CheckCondition(JsonObject? condition, string id)
            {
                if (condition?["topLevelId"] is null) return;
                condition["topLevelId"] = null;
                if (string.IsNullOrWhiteSpace(condition["topLevelTitle"]?.GetValue<string>()))
                    Review("unstable_window_condition", id, "A window condition depended on a transient top-level id and needs a stable title.", true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException)
        { return Fail<WorkflowExportResponse>("workflow_export_failed", "Export failed before producing a usable workflow: " + exception.GetType().Name); }
    }

    private static IEnumerable<CompiledWorkflowNode> Flatten(IEnumerable<CompiledWorkflowNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Primary.Concat(node.Alternate))) yield return child;
        }
    }

    public static async Task<CoreResult<SemanticWorkflowResponse>> ReplayAsync(LocalBridgeClient client, WorkflowReplayRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.SessionId);
            var file = new FileInfo(request.ExportPath);
            if (!file.Exists || file.Length > MaximumBytes) return Fail<SemanticWorkflowResponse>("workflow_export_invalid", "Select an existing AvaScope export.json of at most 1 MiB.");
            var document = JsonSerializer.Deserialize<WorkflowExportDocument>(await File.ReadAllTextAsync(file.FullName, cancellationToken), JsonOptions);
            if (document is null) return Fail<SemanticWorkflowResponse>("workflow_export_invalid", "The export document is empty.");
            var bound = Bind(document, request, enforceReview: true);
            if (!bound.Success) return CoreResult<SemanticWorkflowResponse>.Fail(bound.Error!);
            return await new SemanticWorkflowRunner().RunAsync(client, bound.Value!, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        { return Fail<SemanticWorkflowResponse>("workflow_replay_invalid", "The export or replay bindings are invalid: " + exception.GetType().Name); }
    }

    private static CoreResult<SemanticWorkflowRequest> Bind(WorkflowExportDocument document, WorkflowReplayRequest request, bool enforceReview)
    {
        if (document.SchemaVersion != 1 || document.Parameters is null || document.Parameters.Count > 128
            || document.Parameters.Any(parameter => parameter is null || string.IsNullOrWhiteSpace(parameter.Name))
            || document.Parameters.Select(parameter => parameter.Name).Distinct(StringComparer.Ordinal).Count() != document.Parameters.Count
            || document.ReviewItems is null || document.ReviewItems.Count > 1024 || document.ReviewItems.Any(item => item is null))
            return Fail<SemanticWorkflowRequest>("workflow_export_invalid", "Unsupported or unbounded export document.");
        if (enforceReview && (document.ReviewItems.Any(item => item.BlocksReplay) || document.ReviewItems.Count > 0 && !request.AcknowledgeReview))
            return Fail<SemanticWorkflowRequest>("workflow_export_review_required", "Resolve blocking items by correcting the source and exporting again. Explicitly acknowledge any remaining review items before validation/replay.");
        var parameters = request.Parameters ?? new Dictionary<string, string>();
        if (parameters.Count != document.Parameters.Count || document.Parameters.Any(parameter => !parameters.ContainsKey(parameter.Name)) || parameters.Values.Any(value => value is null || value.Length > 8192))
            return Fail<SemanticWorkflowRequest>("workflow_export_parameters_required", "Supply exactly the named export parameters; recorded values are never stored as defaults.");
        if (document.RequiresIsolatedState && string.IsNullOrWhiteSpace(request.IsolatedStateDirectory)
            || document.RequiresEvidenceRoot && string.IsNullOrWhiteSpace(request.EvidenceRoot)
            || document.RequiresProcessId && request.AuthorizedProcessId is not > 0)
            return Fail<SemanticWorkflowRequest>("workflow_export_authorization_required", "Explicitly rebind the required isolated state, evidence root and/or authorized process id.");
        var workflow = JsonNode.Parse(document.Workflow.GetRawText())!.AsObject();
        ReplaceStrings(workflow, value => Tokens.Replace(value, match => parameters.TryGetValue(match.Groups["name"].Value, out var supplied) ? supplied : match.Value));
        workflow["sessionId"] = request.SessionId.Value;
        workflow["requestId"] = Guid.NewGuid().ToString("N");
        workflow["outputDirectory"] = Path.GetFullPath(request.OutputDirectory);
        workflow["isolatedStateDirectory"] = request.IsolatedStateDirectory is null ? null : Path.GetFullPath(request.IsolatedStateDirectory);
        workflow["validateOnly"] = request.ValidateOnly;
        workflow["allowDestructive"] = request.AllowDestructive && workflow["allowDestructive"]?.GetValue<bool>() == true;
        if (workflow["evidence"]?["policy"] is JsonObject policy)
        {
            policy["ownedEvidenceRoot"] = request.EvidenceRoot;
            if (policy["authorizedSessionIds"]?.AsArray().Count > 0) policy["authorizedSessionIds"] = new JsonArray(request.SessionId.Value);
            if (policy["authorizedProcessIds"]?.AsArray().Count > 0) policy["authorizedProcessIds"] = new JsonArray(request.AuthorizedProcessId);
        }
        if (workflow["topLevelAliases"] is JsonArray aliases)
            foreach (var alias in aliases.OfType<JsonObject>())
                if (alias["selector"]?["sessionId"] is not null) alias["selector"]!["sessionId"] = request.SessionId.Value;
        var bound = workflow.Deserialize<SemanticWorkflowRequest>(JsonOptions);
        return bound is null ? Fail<SemanticWorkflowRequest>("workflow_export_invalid", "The workflow template is invalid.") : CoreResult<SemanticWorkflowRequest>.Ok(bound);
    }

    private static bool IsControl(string action) => action is SemanticWorkflowActions.Click or SemanticWorkflowActions.TypeText or SemanticWorkflowActions.ClearText or SemanticWorkflowActions.Focus
        or SemanticWorkflowActions.Invoke or SemanticWorkflowActions.Select or SemanticWorkflowActions.Toggle or SemanticWorkflowActions.Expand
        or SemanticWorkflowActions.Collapse or SemanticWorkflowActions.KeyDown or SemanticWorkflowActions.KeyUp or SemanticWorkflowActions.KeySequence
        or SemanticWorkflowActions.Drag or SemanticWorkflowActions.Swipe or SemanticWorkflowActions.LongPress or SemanticWorkflowActions.PressAndHold
        or SemanticWorkflowActions.CustomAction or SemanticWorkflowActions.PickerResult;

    private static string ReplaceLiterals(string value, IReadOnlyDictionary<string, string> values)
    {
        var replacements = values.OrderByDescending(pair => pair.Value.Length).ToArray();
        var parts = Regex.Split(value, @"(\$\{[A-Za-z_][A-Za-z0-9_.-]*\})", RegexOptions.CultureInvariant);
        for (var i = 0; i < parts.Length; i += 2)
        {
            var original = parts[i];
            var result = new StringBuilder();
            var offset = 0;
            while (offset < original.Length)
            {
                var next = original.Length;
                KeyValuePair<string, string>? selected = null;
                foreach (var pair in replacements)
                {
                    var found = original.IndexOf(pair.Value, offset, StringComparison.Ordinal);
                    if (found >= 0 && found < next) { next = found; selected = pair; }
                }
                result.Append(original.AsSpan(offset, next - offset));
                if (selected is not { } replacement) break;
                result.Append("${").Append(replacement.Key).Append('}');
                offset = next + replacement.Value.Length;
            }
            parts[i] = result.ToString();
        }
        return string.Concat(parts);
    }

    private static void ReplaceStrings(JsonNode node, Func<string, string> replace)
    {
        if (node is JsonObject map)
            foreach (var pair in map.ToArray())
            {
                if (pair.Value is JsonValue value && value.TryGetValue<string>(out var text)) map[pair.Key] = replace(text);
                else if (pair.Value is not null) ReplaceStrings(pair.Value, replace);
            }
        else if (node is JsonArray array)
            for (var i = 0; i < array.Count; i++)
            {
                if (array[i] is JsonValue value && value.TryGetValue<string>(out var text)) array[i] = replace(text);
                else if (array[i] is { } child) ReplaceStrings(child, replace);
            }
    }
    private static CoreResult<T> Fail<T>(string code, string message) => CoreResult<T>.Fail(new(code, message));
}
