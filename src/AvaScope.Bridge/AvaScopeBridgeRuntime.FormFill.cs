using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private readonly Dictionary<string, (string Fingerprint, CoreResult<RuntimeFormFillResponse>? Result)> _formRequests = new(StringComparer.Ordinal);

    public async Task<CoreResult<RuntimeFormFillResponse>> FillFormAsync(RuntimeFormFillRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await _explicitInputGate.WaitAsync(0, cancellationToken))
            return CoreResult<RuntimeFormFillResponse>.Fail(new("form_fill_busy", "Another compound input operation is active; wait for its result."));
        try
        {
            var task = await Dispatcher.UIThread.InvokeAsync<Task<CoreResult<RuntimeFormFillResponse>>>(() => FillForm(request, cancellationToken), DispatcherPriority.Background, cancellationToken);
            return await task;
        }
        finally { _explicitInputGate.Release(); }
    }

    private async Task<CoreResult<RuntimeFormFillResponse>> FillForm(RuntimeFormFillRequest request, CancellationToken cancellationToken)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (request.Form.SessionId != SessionId) return Fail("form_session_mismatch", "Select this bridge session explicitly.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
        if (bytes.Length > 65536) return Fail("form_request_limit", "Fill requests are limited to 64 KiB.");
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (_formRequests.TryGetValue(request.RequestId, out var previous))
        {
            if (previous.Fingerprint != fingerprint) return Fail("form_request_conflict", "This fill id belongs to a different payload; inspect the original result before making a new intent.");
            if (previous.Result is null) return Fail("form_fill_pending", "This fill has started and its outcome is pending. Do not dispatch it with a new id.");
            return previous.Result.Success ? CoreResult<RuntimeFormFillResponse>.Ok(previous.Result.Value! with { Replayed = true }) : previous.Result;
        }
        if (_formRequests.Count == 64) return Fail("form_ledger_full", "This session retains 64 fill results and cannot safely accept another id. Existing ids remain replayable.");
        _formRequests.Add(request.RequestId, (fingerprint, null));
        var results = new List<RuntimeFormFieldResult>();
        var diagnostics = new List<ProtocolError>();
        RuntimeFormInspectionResponse? before = null;
        RuntimeFormInspectionResponse? after = null;
        var plans = new List<(RuntimeFormFieldInput Input, RuntimeFormField Field, RuntimeDesiredStateRequest Request, string Identity, RuntimeDesiredStateValue State)>();
        var dispatched = false;
        var status = "invalid_plan";
        var started = Stopwatch.GetTimestamp();
        try
        {
            var inspected = InspectForm(request.Form);
            if (!inspected.Success) { diagnostics.Add(Error(inspected.Error!)); return Complete(); }
            before = inspected.Value!;
            if (before.Coverage.Reasons.Any(reason => reason is "generation_changed" or "analysis_budget" or "node_limit" or "depth_limit" or "field_limit" or "response_byte_limit"))
            { diagnostics.Add(new("form_inventory_incomplete", "The bounded form inventory cannot support complete plan validation. Select a smaller scope or increase its limits.")); return Complete(); }
            var top = FindTopLevel(request.Form.TopLevelId)!;
            foreach (var input in request.Fields)
            {
                CheckBudget();
                try
                {
                    var query = QueryNodes(new(SessionId, request.Form.TopLevelId, input.Selector, maxResults: 2, maxNodes: request.Form.MaxNodes, maxDepth: 32, policy: request.Form.Policy));
                    if (!query.Success || query.Value!.Matches.Count != 1 || query.Value.Coverage!.Reasons.Any(reason => reason != "policy_exclusions"))
                    {
                        results.Add(Rejected(input.Id, "form_field_unresolved", "The selector must resolve one complete, unambiguous field. Refine its identity or explicit label/container relationship."));
                        continue;
                    }
                    var match = query.Value.Matches[0];
                    var field = before.Fields.SingleOrDefault(item => item.Target.NodeId == match.Target!.NodeId);
                    if (field is null || FindNodeById(top, match.Target!.NodeId!) is not Control control)
                    { results.Add(Rejected(input.Id, "form_field_outside_scope", "The matched node is not a supported field in the inspected scope.")); continue; }
                    if (field.Sensitive && !request.AllowSensitiveInput)
                    { results.Add(Rejected(input.Id, "form_sensitive_input_denied", "Explicitly allow sensitive input for this plan; field values remain redacted.")); continue; }
                    if (plans.Any(plan => plan.Field.Target.NodeId == field.Target.NodeId))
                    { results.Add(Rejected(input.Id, "form_duplicate_target", "Multiple mappings address the same field.")); continue; }
                    if (control is TextBox { MaxLength: > 0 } box && input.Desired.ValueKind == JsonValueKind.String && input.Desired.GetString()!.Length > box.MaxLength)
                    { results.Add(Rejected(input.Id, "form_text_limit", "The value exceeds the TextBox's declared maximum length.")); continue; }
                    if (control is ToggleButton { IsThreeState: false, IsChecked: not null } && input.Desired.ValueKind == JsonValueKind.Null)
                    { results.Add(Rejected(input.Id, "form_check_constraints", "A two-state control cannot reach an indeterminate state through its toggle interaction.")); continue; }
                    var identity = QueryIdentity(control);
                    var id = "form-" + fingerprint[..32] + "-" + plans.Count;
                    var desired = new RuntimeDesiredStateRequest(match.Target!, field.Property, input.Desired, id, request.Form.Policy);
                    if (request.Form.Policy is { } policy && !policy.AllowedDesiredStates.Contains(field.Property, StringComparer.Ordinal))
                    { results.Add(Rejected(input.Id, "form_policy_denied", "The policy must allow this field's desired-state property.")); continue; }
                    var validation = EnsureState(desired, validateOnly: true);
                    if (!validation.Success || validation.Value!.Status is not ("validated" or "already_satisfied") || QueryIdentity(control) != identity)
                    {
                        results.Add(new(input.Id, field.Target, "rejected", false, null, validation.Success && validation.Value!.Diagnostics.Count > 0
                            ? validation.Value.Diagnostics : [new("form_field_not_writable", "The field failed provider/state/target validation before dispatch.")]));
                        continue;
                    }
                    plans.Add((input, field, desired, identity, validation.Value.Before));
                }
                catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
                { results.Add(Rejected(input.Id, "form_invalid_value", "The field value or target is incompatible with its supported desired-state operation.")); }
            }
            if (results.Count > 0) return Complete();
            // Reserve capacity for the complete plan before any field can dispatch.
            if (_desiredStateRequests.Count + plans.Count > 512)
            { diagnostics.Add(new("form_desired_state_capacity", "The session cannot retain the whole field plan safely. No input was dispatched.")); return Complete(); }
            if (plans.Any(plan => _desiredStateRequests.ContainsKey(plan.Request.RequestId)))
            { diagnostics.Add(new("form_field_request_conflict", "A field request id was already reserved outside this plan. No input was dispatched.")); return Complete(); }
            status = "passed";
            foreach (var plan in plans)
            {
                CheckBudget();
                var validation = EnsureState(plan.Request, validateOnly: true);
                if (FindNodeById(top, plan.Field.Target.NodeId!) is not Control control || QueryIdentity(control) != plan.Identity
                    || !validation.Success || validation.Value!.Status is not ("validated" or "already_satisfied")
                    || !SameState(plan.State, validation.Value.Before))
                {
                    results.Add(new(plan.Input.Id, plan.Field.Target, "plan_changed", false, null,
                        [new("form_plan_changed", "The field identity, value or writable state changed after prevalidation. Inspect dependencies and create a new plan.")]));
                    status = dispatched ? "partial" : "invalid_plan"; break;
                }
                var execution = RememberDesiredState(plan.Request);
                if (!execution.Success)
                {
                    var uncertain = execution.Error!.Code == "desired_state_uncertain";
                    results.Add(new(plan.Input.Id, plan.Field.Target, uncertain ? "uncertain" : "failed", false, null, [Error(execution.Error)]));
                    status = uncertain ? "uncertain" : dispatched ? "partial" : "failed"; break;
                }
                var value = execution.Value!;
                dispatched |= value.DispatchedOperations > 0 || value.PreparationPerformed;
                var index = results.Count;
                results.Add(new(plan.Input.Id, plan.Field.Target, value.Status, value.Verified,
                    value with { Target = plan.Field.Target, Before = Compact(value.Before), After = Compact(value.After) }, value.Diagnostics));
                if (!value.Verified) { status = value.Status == "uncertain" ? "uncertain" : "partial"; break; }
                if (request.SettleMs > 0) await Task.Delay(request.SettleMs, cancellationToken);
                var verified = EnsureState(plan.Request, validateOnly: true);
                if (!verified.Success || verified.Value!.Status != "already_satisfied" || !verified.Value.Verified)
                {
                    results[index] = results[index] with { Status = "not_verified", Verified = false,
                        Diagnostics = verified.Success && verified.Value!.Diagnostics.Count > 0 ? verified.Value.Diagnostics
                            : [new("form_postcondition_changed", "The field no longer satisfies its state/validation after the bounded settling interval.")] };
                    status = "partial"; break;
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            status = dispatched ? "uncertain" : exception is OperationCanceledException ? "cancelled" : "failed";
            diagnostics.Add(new(exception is OperationCanceledException ? "form_fill_cancelled" : "form_fill_failed",
                "Filling stopped. Earlier field effects are retained; retrieve this result and inspect current state before a new intent."));
        }
        return Complete();

        CoreResult<RuntimeFormFillResponse> Complete()
        {
            // A later field may invalidate an earlier one. Recheck all completed outcomes without dispatching.
            foreach (var plan in plans)
            {
                var index = results.FindIndex(result => result.Id == plan.Input.Id && result.Verified);
                if (index < 0) continue;
                var final = EnsureState(plan.Request, validateOnly: true);
                if (!final.Success || final.Value!.Status != "already_satisfied" || !final.Value.Verified)
                {
                    results[index] = results[index] with { Verified = false, Status = "not_verified",
                        Diagnostics = [new("form_final_postcondition_changed", "The field no longer satisfies its requested state or validation at the final observation. Earlier effects were not rolled back.")] };
                    if (status == "passed") status = "partial";
                }
            }
            var observed = InspectForm(request.Form);
            if (observed.Success) after = observed.Value;
            else { diagnostics.Add(Error(observed.Error!)); if (status == "passed") status = "uncertain"; }
            foreach (var field in request.Fields.Where(field => results.All(result => result.Id != field.Id)))
                results.Add(new(field.Id, plans.FirstOrDefault(plan => plan.Input.Id == field.Id).Field?.Target, "not_executed", false, null, []));
            var oldFields = (before?.Fields ?? []).ToDictionary(field => field.Target.NodeId!, StringComparer.Ordinal);
            var newFields = (after?.Fields ?? []).ToDictionary(field => field.Target.NodeId!, StringComparer.Ordinal);
            var appeared = newFields.Keys.Except(oldFields.Keys, StringComparer.Ordinal).ToArray();
            var disappeared = oldFields.Keys.Except(newFields.Keys, StringComparer.Ordinal).ToArray();
            var changed = newFields.Keys.Intersect(oldFields.Keys, StringComparer.Ordinal).Where(id =>
                FieldSignature(newFields[id]) != FieldSignature(oldFields[id])).ToArray();
            if (after?.Coverage.Reasons.Any(reason => reason is "generation_changed" or "analysis_budget") == true && status == "passed")
                status = "uncertain";
            var response = new RuntimeFormFillResponse(request.RequestId, status, before, after,
                request.Fields.Select(field => results.Single(result => result.Id == field.Id)).ToArray(), appeared, disappeared, changed, diagnostics, DateTimeOffset.UtcNow);
            CoreResult<RuntimeFormFillResponse> result = CoreResult<RuntimeFormFillResponse>.Ok(response);
            if (request.Form.Policy is { } policy) result = new RuntimeEvidencePolicyEnforcer(policy).Sanitize(response);
            _formRequests[request.RequestId] = (fingerprint, result);
            return result;
        }
        void CheckBudget()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(started) > TimeSpan.FromSeconds(30))
                throw new OperationCanceledException("The cooperative form budget expired.");
        }
        static RuntimeDesiredStateValue Compact(RuntimeDesiredStateValue value) => value.Value is { } json && json.GetRawText().Length > 3072
            ? value with { Status = "omitted_response_budget", Value = null } : value;
        static CoreResult<RuntimeFormFillResponse> Fail(string code, string message) => CoreResult<RuntimeFormFillResponse>.Fail(new(code, message));
        static ProtocolError Error(CoreError error) => new(error.Code, error.Message, error.Details);
        static RuntimeFormFieldResult Rejected(string id, string code, string message) => new(id, null, "rejected", false, null, [new(code, message)]);
    }

    private static bool SameState(RuntimeDesiredStateValue left, RuntimeDesiredStateValue right) => left.Status == right.Status
        && JsonElement.DeepEquals(left.Value ?? JsonSerializer.SerializeToElement<object?>(null), right.Value ?? JsonSerializer.SerializeToElement<object?>(null));

    private static string FieldSignature(RuntimeFormField field) => JsonSerializer.Serialize(new
    {
        field.Label, field.LabelSource, field.State, field.Writable, field.Validation, field.ChoiceCount, field.ChoicesComplete,
        choices = field.Choices.Select(choice => new { choice.Index, choice.Label, choice.Selected, nodeId = choice.Target?.NodeId })
    });
}
