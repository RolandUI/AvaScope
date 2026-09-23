using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    // Retain every accepted edit id: an uncertain edit must never be repeated after response loss.
    private readonly ConcurrentDictionary<string, (string Fingerprint, CoreResult<RuntimeTextEditResponse>? Result)> _textEditRequests = new(StringComparer.Ordinal);
    private bool _textEditingClosed;

    public async Task<CoreResult<RuntimeTextEditResponse>> EditTextAsync(RuntimeTextEditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var editing = request.Action != "read";
        if (editing && !await _explicitInputGate.WaitAsync(0, cancellationToken))
            return TextEditFailure("text_edit_busy", "Another compound input operation is active; wait for its result.");
        try { return await Dispatcher.UIThread.InvokeAsync(() => RememberTextEdit(request), DispatcherPriority.Background, cancellationToken); }
        finally { if (editing) _explicitInputGate.Release(); }
    }

    private void CloseTextEditing()
    {
        Volatile.Write(ref _textEditingClosed, true);
        _textEditRequests.Clear();
    }

    private CoreResult<RuntimeTextEditResponse> RememberTextEdit(RuntimeTextEditRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (Volatile.Read(ref _textEditingClosed)) return TextEditFailure("text_edit_session_closed", "This bridge session has closed.");
        if (request.Target.SessionId != SessionId) return TextEditFailure("text_edit_session_mismatch", "Select this bridge session explicitly.");
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy?.AuthorizeAction(SemanticWorkflowActions.Inspect, null) is { Success: false } denied)
            return CoreResult<RuntimeTextEditResponse>.Fail(denied.Error!);
        if (request.Action != "read" && request.Policy is { } options && !options.AllowedDesiredStates.Contains("text", StringComparer.Ordinal))
            return TextEditFailure("text_edit_policy_denied", "Selection and editing require the policy's explicit allowedDesiredStates: text permission.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
        if (bytes.Length > 65536) return TextEditFailure("text_edit_request_limit", "The request exceeds 64 KiB.");
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (request.RequestId is { } id)
        {
            if (_textEditRequests.TryGetValue(id, out var existing))
            {
                if (existing.Fingerprint != fingerprint) return TextEditFailure("text_edit_id_conflict", "This id belongs to a different payload. Inspect the previous result before making a new request.");
                if (existing.Result is null) return TextEditFailure("text_edit_pending", "This edit started but has no retained outcome. Do not repeat it with a new id.");
                return existing.Result.Success ? CoreResult<RuntimeTextEditResponse>.Ok(existing.Result.Value! with { Replayed = true }) : existing.Result;
            }
            if (_textEditRequests.Count >= 128) return TextEditFailure("text_edit_ledger_full", "The session reached its 128-edit limit. Existing ids remain replayable; reads remain available.");
            _textEditRequests[id] = (fingerprint, null);
        }
        CoreResult<RuntimeTextEditResponse> result;
        try { result = EditText(request, policy); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        { result = TextEditFailure("text_edit_uncertain", "The editing path failed outside verification. Observe current state; the same request id will not dispatch again."); }
        if (policy is not null)
        {
            if (result.Success) result = policy.Sanitize(result.Value!);
            else
            {
                var safe = policy.Sanitize(result.Error!);
                result = CoreResult<RuntimeTextEditResponse>.Fail(safe.Success ? safe.Value! : safe.Error!);
            }
        }
        if (request.RequestId is { } requestId && !Volatile.Read(ref _textEditingClosed))
            _textEditRequests.TryUpdate(requestId, (fingerprint, result), (fingerprint, null));
        return result;
    }

    private CoreResult<RuntimeTextEditResponse> EditText(RuntimeTextEditRequest request, RuntimeEvidencePolicyEnforcer? policy)
    {
        var target = request.Target;
        var top = FindTopLevel(target.TopLevelId);
        if (top is null) return TopLevelNotFound<RuntimeTextEditResponse>(target.TopLevelId);
        var resolved = ResolveInputGenerationTarget(top, target.TopLevelId, target.NodeId, target);
        if (!resolved.Success) return CoreResult<RuntimeTextEditResponse>.Fail(resolved.Error!);
        if (FindNodeById(top, target.NodeId!) is not TextBox box)
            return TextEditFailure("text_edit_unsupported", "Only public Avalonia TextBox editing is supported. Rich/custom editors require their own explicit app action.");

        var started = Stopwatch.GetTimestamp();
        var identity = QueryIdentity(box, false);
        RuntimeTextState? before = null, after = null;
        var operations = 0;
        var preparation = false;
        try
        {
            before = after = Read();
            if (request.Action == "read") return Finish("observed", true);
            if (before.Revision != request.ExpectedRevision) throw new TextEditStop("text_edit_stale", "Text, caret, selection or editing constraints changed. Read again before creating a new intent.");
            if (box.IsReadOnly) throw new TextEditStop("text_edit_read_only", "This text control is read-only.");
            var start = request.Action == "replace_selection" ? Math.Min(before.SelectionStart, before.SelectionEnd) : request.Start!.Value;
            var end = request.Action == "replace_selection" ? Math.Max(before.SelectionStart, before.SelectionEnd)
                : request.Action == "insert" ? start : request.End!.Value;
            CheckBoundary(start); CheckBoundary(end);
            var replacement = request.Text ?? string.Empty;
            var expectedText = request.Action == "select_range" ? before.Text : before.Text[..start] + replacement + before.Text[end..];
            if (expectedText.Length > RuntimeTextEditRequest.MaximumTextLength
                || request.Action != "select_range" && box.MaxLength > 0 && expectedText.Length > box.MaxLength)
                throw new TextEditStop("text_edit_length", "The result exceeds the bounded text size or the control's MaxLength; no partial replacement was sent.");
            if (policy is not null && policy.SanitizeScalar(expectedText) != expectedText)
                throw new TextEditStop("text_edit_sensitive", "The resulting text is protected by the policy; range editing is unavailable.");

            CheckAction();
            if (Read().Revision != before.Revision) throw Stale();
            if (!box.IsFocused)
            {
                preparation = true;
                if (!box.Focus(NavigationMethod.Unspecified)) throw new TextEditStop("text_edit_focus", "The text control did not accept focus.");
            }
            CheckAction();
            if (Read().Revision != before.Revision) throw Stale();
            // Preserve property bindings. Setting the public caret can collapse selection, so set it first.
            preparation = true;
            box.SetCurrentValue(TextBox.CaretIndexProperty, end);
            CheckPreparedText();
            box.SetCurrentValue(TextBox.SelectionStartProperty, start);
            CheckPreparedText();
            box.SetCurrentValue(TextBox.SelectionEndProperty, end);
            CheckAction();
            CheckPreparedText();
            after = Read();
            if (!box.IsFocused || after.Text != before.Text || after.Caret != end || after.SelectionStart != start || after.SelectionEnd != end)
                throw Stale();
            if (request.Action == "select_range") return Finish("verified", true);
            if (start == end && replacement.Length == 0) return Finish("verified", !after.HasValidationErrors);

            if (replacement.Length > 0)
            {
                operations++;
                box.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Source = box, Text = replacement });
            }
            else
            {
                // An empty selection must never send Delete: that would remove the following character.
                try { operations++; box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = box, Key = Key.Delete }); }
                finally { operations++; box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Source = box, Key = Key.Delete }); }
            }
            after = Read();
            var expectedCaret = start + replacement.Length;
            var verified = after.Text == expectedText && after.Caret == expectedCaret
                && after.SelectionStart == expectedCaret && after.SelectionEnd == expectedCaret && !after.HasValidationErrors;
            return verified ? Finish("verified", true) : Finish("not_verified", false,
                after.HasValidationErrors ? "text_edit_validation_errors" : "text_edit_not_verified",
                "The control did not accept the exact requested text/caret/selection without validation errors. Inspect the observed result before another edit.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            try { after = Read(); }
            catch (Exception readException) when (readException is not OutOfMemoryException and not AccessViolationException)
            { after = null; if (readException is TextEditStop { Code: "text_edit_sensitive" }) before = null; }
            if (exception is TextEditStop { Code: "text_edit_sensitive" }) before = after = null;
            return Finish(operations > 0 || preparation ? "uncertain" : "rejected", false,
                exception is TextEditStop stop ? stop.Code : "text_edit_callback_failed",
                exception is TextEditStop ? exception.Message : "An application callback failed; text may have changed. Observe before choosing another intent.");
        }

        void Fresh()
        {
            if (Volatile.Read(ref _textEditingClosed)) throw new TextEditStop("text_edit_session_closed", "The bridge session closed during editing.");
            if (Stopwatch.GetElapsedTime(started) > TimeSpan.FromSeconds(2)) throw new TextEditStop("text_edit_budget", "The cooperative two-second editing budget expired.");
            if (!ResolveInputGenerationTarget(top, target.TopLevelId, target.NodeId, target).Success
                || TopLevel.GetTopLevel(box) != top || identity != QueryIdentity(box, false))
                throw new TextEditStop("text_edit_target_changed", "The target detached or was recycled during preparation or editing.");
            if (box.PasswordChar != '\0') throw new TextEditStop("text_edit_sensitive", "Password fields do not expose text ranges, state revisions or editing through this tool.");
            if (request.Policy is { } options)
            {
                var ancestors = box.GetSelfAndVisualAncestors().Take(65).ToArray();
                if (ancestors.Length > 64 || ancestors.Any(node => options.ExcludedControlAutomationIds.Contains(GetAutomationId(node), StringComparer.Ordinal)
                    || options.RedactedAutomationIds.Contains(GetAutomationId(node), StringComparer.Ordinal)))
                    throw new TextEditStop("text_edit_sensitive", "The field or an ancestor is protected by policy; its entire text state is withheld.");
            }
        }

        RuntimeTextState Read()
        {
            Fresh();
            var text = box.Text ?? string.Empty;
            if (text.Length > RuntimeTextEditRequest.MaximumTextLength || !RuntimeTextEditRequest.IsWellFormedUtf16(text)
                || box.NewLine.Length > 16 || !RuntimeTextEditRequest.IsWellFormedUtf16(box.NewLine))
                throw new TextEditStop("text_edit_state_unavailable", "Text exceeds 8192 UTF-16 units, NewLine exceeds 16 units or UTF-16 is malformed; no truncated ranges are offered.");
            if (policy is not null && (policy.SanitizeScalar(text) != text || policy.SanitizeScalar(box.NewLine) != box.NewLine))
                throw new TextEditStop("text_edit_sensitive", "The text contains protected content; its entire state is withheld.");
            var state = new RuntimeTextState(text, box.CaretIndex, box.SelectionStart, box.SelectionEnd, "",
                box.IsReadOnly, DataValidationErrors.GetHasErrors(box), box.AcceptsReturn, box.MaxLength, box.NewLine);
            var revision = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { target.SessionId, target.TopLevelId, target.NodeId, target.TopLevelGeneration, target.NodeGeneration, State = state })));
            return state with { Revision = revision };
        }

        void CheckAction()
        {
            if (InputBlocker(top, target.TopLevelId, box, InputActions.KeyText) is not null)
                throw new TextEditStop("text_edit_blocked", "The field is blocked; use explain_action and inspect current state.");
            Fresh();
            if (box.IsReadOnly) throw new TextEditStop("text_edit_read_only", "The text control became read-only.");
        }

        void CheckPreparedText()
        {
            var current = Read();
            if (current.Text != before!.Text || current.IsReadOnly != before.IsReadOnly || current.MaxLength != before.MaxLength
                || current.AcceptsReturn != before.AcceptsReturn || current.NewLine != before.NewLine || current.HasValidationErrors != before.HasValidationErrors)
                throw Stale();
        }

        void CheckBoundary(int index)
        {
            if (index > before!.Text.Length || index > 0 && index < before.Text.Length
                && (char.IsHighSurrogate(before.Text[index - 1]) && char.IsLowSurrogate(before.Text[index])
                    || before.Text[index - 1] == '\r' && before.Text[index] == '\n'))
                throw new TextEditStop("text_edit_range", "A range is outside the observed text or splits a surrogate pair/CRLF sequence.");
        }

        CoreResult<RuntimeTextEditResponse> Finish(string status, bool verified, string? code = null, string? message = null)
        {
            // Selector hints may contain private text; the generation-pinned identity is sufficient here.
            var evidenceTarget = new RuntimeTargetContext(target.SessionId, target.TopLevelId, target.TreeKind, target.NodeId,
                target.CapturedAt, target.TargetKind, target.TopLevelGeneration, target.NodeGeneration);
            IReadOnlyList<ProtocolError> diagnostics = code is null ? [] : [new(code, message!, new Dictionary<string, string>
            { ["nextAction"] = "Read current state. Replay the identical edit id and payload only to retrieve its retained result; use a new id only for a newly observed intent." })];
            return CoreResult<RuntimeTextEditResponse>.Ok(new(request.RequestId, evidenceTarget, request.Action, status, verified, before, after,
                operations, preparation, RuntimePlatformEvidence.Operation(top, operations > 0 ? RuntimeOperationRoutes.RoutedEvent
                    : preparation ? RuntimeOperationRoutes.ControlProperty : RuntimeOperationRoutes.NotDispatched, operations > 0 || preparation), diagnostics, DateTimeOffset.UtcNow));
        }

        static TextEditStop Stale() => new("text_edit_stale", "Text or selection changed during preparation; the old range was not dispatched.");
    }

    private static CoreResult<RuntimeTextEditResponse> TextEditFailure(string code, string message) => CoreResult<RuntimeTextEditResponse>.Fail(new(code, message));
    private sealed class TextEditStop(string code, string message) : Exception(message) { public string Code { get; } = code; }
}
