using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public async Task<CoreResult<FindNodesResponse>> QueryNodesAsync(RuntimeQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await Dispatcher.UIThread.InvokeAsync(() => QueryNodes(request), DispatcherPriority.Background, cancellationToken);
    }

    private CoreResult<FindNodesResponse> QueryNodes(RuntimeQueryRequest request, bool requireCollectionCoverage = false)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (request.SessionId != SessionId) return InvalidFindRequest("Query belongs to a different session.");
        var top = FindTopLevel(request.TopLevelId);
        if (top is null) return TopLevelNotFound<FindNodesResponse>(request.TopLevelId);
        var treeKind = request.Selector.TreeKind;
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        var nodes = new List<QueryNode>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var started = Stopwatch.GetTimestamp();
        var work = 0;
        bool Budget()
        {
            if (++work > 32768 || Stopwatch.GetElapsedTime(started) > TimeSpan.FromSeconds(2))
            { reasons.Add("analysis_budget"); return false; }
            return true;
        }
        IEnumerable<object> Children(object node) => treeKind == TreeKinds.Visual
            ? node is Visual visual ? visual.GetVisualChildren().Cast<object>() : []
            : node is ILogical logical ? logical.GetLogicalChildren().Cast<object>().Distinct(ReferenceEqualityComparer.Instance) : [];
        void Visit(object node, QueryNode? parent, int depth)
        {
            if (visited.Contains(node)) return;
            if (!Budget()) return;
            if (nodes.Count == request.MaxNodes) { reasons.Add("node_limit"); return; }
            visited.Add(node);
            var excluded = parent?.Excluded == true || request.Policy?.ExcludedControlAutomationIds.Contains(GetAutomationId(node), StringComparer.Ordinal) == true
                || requireCollectionCoverage && request.Policy?.RedactedAutomationIds.Contains(GetAutomationId(node), StringComparer.Ordinal) == true;
            var entry = new QueryNode(node, parent, depth, excluded, QueryIdentity(node));
            nodes.Add(entry);
            if (excluded) { reasons.Add("policy_exclusions"); return; }
            if (requireCollectionCoverage && request.Selector.NodeId is null && node is ItemsControl items && items.ItemCount > items.GetRealizedContainers().Take(request.MaxNodes + 1).Count())
                reasons.Add("unrealized_collection_items");
            if (requireCollectionCoverage && request.Selector.NodeId is null && DataGridTable.FindType(node.GetType(), "Avalonia.Controls.DataGrid") is not null)
                reasons.Add("table_requires_structured_query");
            if (depth == request.MaxDepth)
            {
                if (Children(node).Any(child => !visited.Contains(child))) reasons.Add("depth_limit");
                return;
            }
            var children = Children(node).Where(child => !visited.Contains(child)).Take(request.MaxNodes + 1).ToArray();
            entry.Children = children;
            foreach (var child in children)
            {
                if (nodes.Count == request.MaxNodes) { reasons.Add("node_limit"); break; }
                Visit(child, entry, depth + 1);
            }
        }
        Visit(top, null, 0);
        var byObject = nodes.ToDictionary(entry => entry.Node, ReferenceEqualityComparer.Instance);
        var matches = new List<FindNodeMatch>();
        var projections = new List<RuntimeQueryProjection>();
        var candidates = new List<RuntimeQueryCandidate>();
        var matched = 0;
        foreach (var entry in nodes)
        {
            if (!Budget()) break;
            var evidence = new List<RuntimeRelationshipEvidence>();
            if (!Match(entry, request.Selector, evidence, 0))
            {
                if (candidates.Count < 8 && request.Selector.Relationships.Count > 0 && Match(entry, request.Selector, [], 0, relationships: false))
                    candidates.Add(new(CreateNodeTarget(request.TopLevelId, treeKind, top, entry.Node), entry.Node.GetType().FullName!,
                        Safe(GetName(entry.Node)), Safe(GetAutomationId(entry.Node)),
                        "Identity/state matched, but no proven relationship match was found within the query scope: " + string.Join(",", request.Selector.Relationships.Select(relation => relation.Kind))));
                continue;
            }
            matched++;
            if (matched > request.MaxResults) { reasons.Add("result_limit"); break; }
            var selection = CopyQuerySelector(request.Selector, request.MaxDepth);
            var target = CreateNodeTarget(request.TopLevelId, treeKind, top, entry.Node);
            var revision = SelectionRevision(entry, evidence);
            target = new(target.SessionId, target.TopLevelId, target.TreeKind, target.NodeId, target.CapturedAt, target.TargetKind,
                target.TopLevelGeneration, target.NodeGeneration, selection, revision);
            if (request.Attributes.Count > 0)
                projections.Add(new(target, request.Attributes.Select(attribute => Project(entry, attribute)).ToArray(), evidence));
            else
            {
                var summary = Summary(entry);
                matches.Add(new(new(summary.NodeId, summary.NodeType, Safe(summary.Name), Safe(summary.AutomationId), Safe(summary.Text),
                    summary.Bounds, target: target, interactionState: summary.InteractionState),
                    Ancestors(entry, includeSelf: true).Reverse().Select(item => CreateNodeId(item.Node, treeKind)).ToArray(), target, evidence));
            }
        }
        foreach (var captured in matches.Select(match => (match.Target!, match.Relationships))
            .Concat(projections.Select(projection => (projection.Target, projection.Relationships))))
        {
            var entry = nodes.First(item => CreateNodeId(item.Node, treeKind) == captured.Item1.NodeId);
            var current = new List<RuntimeRelationshipEvidence>();
            if (!Match(entry, request.Selector, current, 0)
                || SelectionRevision(entry, current) != captured.Item1.SelectionRevision
                || !current.Select(proof => (proof.Kind, proof.Target.NodeId)).SequenceEqual(captured.Relationships.Select(proof => (proof.Kind, proof.Target.NodeId))))
            {
                if (!reasons.Contains("analysis_budget")) reasons.Add("generation_changed");
                matches.Clear(); projections.Clear(); candidates.Clear(); matched = 0; break;
            }
        }
        // Public peer/property callbacks can re-enter application code, including during relationship revalidation.
        foreach (var entry in nodes)
        {
            if (!Budget()) break;
            if (entry.Identity != QueryIdentity(entry.Node)
                || entry.Children is { } captured && !captured.SequenceEqual(Children(entry.Node).Take(request.MaxNodes + 1), ReferenceEqualityComparer.Instance))
            { reasons.Add("generation_changed"); matches.Clear(); projections.Clear(); candidates.Clear(); matched = 0; break; }
        }
        FindNodesResponse Response() => new(SessionId, request.TopLevelId, treeKind, request.MaxDepth, matches.ToArray(),
            CreateTreeTarget(request.TopLevelId, treeKind, top), projections: projections.ToArray(),
            coverage: new(reasons.Count == 0, nodes.Count, matched, reasons.Order(StringComparer.Ordinal).ToArray()), candidates: candidates.ToArray());
        var response = Response();
        if (policy is not null)
        {
            var sanitized = policy.Sanitize(response);
            if (!sanitized.Success) return sanitized;
            response = sanitized.Value!;
        }
        // Query output is bounded in memory; this read-only path never writes unsanitized spill artifacts.
        return CoreResult<FindNodesResponse>.Ok(ResponseBudgeter.Apply(response, maxInlineBytes: 65536, maxItems: 64));

        TreeNodeSummary Summary(QueryNode entry) => entry.Summary ??= new(CreateNodeId(entry.Node, treeKind),
            entry.Node.GetType().FullName ?? entry.Node.GetType().Name, GetName(entry.Node), GetAutomationId(entry.Node), GetText(entry.Node),
            treeKind == TreeKinds.Visual ? GetTreeNodeBounds(top, entry.Node) : GetBounds(entry.Node),
            target: CreateNodeTarget(request.TopLevelId, treeKind, top, entry.Node), interactionState: CreateInteractionState(top, entry.Node));

        string? Safe(string? value)
        {
            if (value is null) return null;
            var safe = policy?.SanitizeScalar(value) ?? value;
            if (safe.Length <= 512) return safe;
            reasons.Add("text_truncated");
            return safe[..512];
        }

        IEnumerable<QueryNode> Ancestors(QueryNode entry, bool includeSelf = false)
        {
            for (var ancestor = includeSelf ? entry : entry.Parent; ancestor is not null; ancestor = ancestor.Parent)
                yield return ancestor;
        }

        bool Match(QueryNode entry, SemanticWorkflowSelector selector, List<RuntimeRelationshipEvidence> evidence, int depth, bool relationships = true)
        {
            if (!Budget() || depth > 4 || entry.Excluded || selector.MaxDepth is { } limit && entry.Depth > limit) return false;
            var summary = Summary(entry);
            if (selector.NodeId is not null && selector.NodeId != summary.NodeId
                || !Matches(summary, selector.NodeType ?? selector.Role, selector.Name, selector.AutomationId, selector.Text,
                    selector.Visible, selector.Enabled, selector.Rendered, selector.Actionable)) return false;
            if (selector.BindingPath is not null || selector.CommandName is not null)
            {
                var map = CreateRuntimeSourceMap(entry.Node, GetComputedProperties(entry.Node));
                if (selector.BindingPath is { } binding && map.Bindings.All(item => item.BindingPath != binding)
                    || selector.CommandName is { } command && map.Bindings.All(item => !item.TargetProperty.Contains("Command", StringComparison.OrdinalIgnoreCase) || item.BindingPath != command)) return false;
            }
            if (!relationships) return true;
            foreach (var relation in selector.Relationships)
            {
                IEnumerable<(QueryNode Node, string Source, int Distance)> candidates = relation.Kind switch
                {
                    "parent" => entry.Parent is { } parent ? [(parent, "public_" + treeKind + "_parent", 1)] : [],
                    "ancestor" => Ancestors(entry).Take(relation.MaxDepth).Select((ancestor, index) => (ancestor, "public_" + treeKind + "_ancestor", index + 1)),
                    "descendant" => nodes.Where(other => other.Depth > entry.Depth && other.Depth - entry.Depth <= relation.MaxDepth
                        && Ancestors(other).Contains(entry)).Select(other => (other, "public_" + treeKind + "_descendant", other.Depth - entry.Depth)),
                    _ => Labels(entry)
                };
                var found = false;
                foreach (var candidate in candidates)
                {
                    var nested = new List<RuntimeRelationshipEvidence>();
                    if (!Match(candidate.Node, relation.Selector, nested, depth + 1)) continue;
                    evidence.Add(new(relation.Kind, candidate.Source, CreateNodeTarget(request.TopLevelId, treeKind, top, candidate.Node.Node), candidate.Distance));
                    evidence.AddRange(nested);
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            return true;
        }

        IEnumerable<(QueryNode Node, string Source, int Distance)> Labels(QueryNode entry)
        {
            if (entry.Node is not Control control) yield break;
            var label = AutomationProperties.GetLabeledBy(control);
            var source = "automation_labeled_by";
            if (label is null)
            {
                try
                {
                    label = (ControlAutomationPeer.CreatePeerForElement(control)?.GetLabeledBy() as ControlAutomationPeer)?.Owner;
                    source = "automation_peer_labeled_by";
                }
                catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
                { reasons.Add("label_provider_unavailable"); }
            }
            if (label is not null)
            {
                if (byObject.TryGetValue(label, out var related)) yield return (related, source, 1);
                else reasons.Add("label_outside_scope");
            }
            foreach (var candidate in nodes)
                if (candidate.Node is Label declared && ReferenceEquals(declared.Target, control))
                    yield return (candidate, "label_target", 1);
        }

        string SelectionRevision(QueryNode entry, IReadOnlyList<RuntimeRelationshipEvidence> evidence)
        {
            var witness = Ancestors(entry, true).Select(item => QueryIdentity(item.Node, includeValues: false)).ToList();
            foreach (var proof in evidence)
                if (nodes.FirstOrDefault(item => CreateNodeId(item.Node, treeKind) == proof.Target.NodeId) is { } related)
                    witness.Add(QueryIdentity(related.Node, includeValues: false));
            return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { SessionId, request.TopLevelId, witness })));
        }

        RuntimeQueryValue Project(QueryNode entry, string attribute)
        {
            var summary = Summary(entry);
            object? value = attribute switch
            {
                "name" => summary.Name, "automationId" => summary.AutomationId, "text" => summary.Text, "nodeType" => summary.NodeType,
                "role" => GetAccessibilityState(entry.Node)?.ControlType,
                "visible" => summary.InteractionState?.Visible, "enabled" => summary.InteractionState?.Enabled,
                "rendered" => summary.InteractionState?.Rendered, "actionable" => summary.InteractionState?.Actionable,
                "focused" => (entry.Node as InputElement)?.IsFocused,
                "checked" => (entry.Node as ToggleButton)?.IsChecked,
                "selected" => entry.Node is ListBoxItem item ? item.IsSelected : entry.Node is TreeViewItem treeItem ? treeItem.IsSelected : null,
                "value" => entry.Node is RangeBase range ? range.Value : entry.Node is TextBox text ? text.Text : null,
                _ => null
            };
            var type = attribute is "visible" or "enabled" or "rendered" or "actionable" or "focused" or "checked" or "selected"
                ? "boolean" : attribute == "value" && entry.Node is RangeBase ? "number" : "string";
            var status = value is null ? attribute == "checked" && entry.Node is ToggleButton ? "indeterminate" : "missing" : "present";
            if (value is double number && !double.IsFinite(number)) { value = null; status = "unavailable"; }
            if (value is string textValue)
            {
                var safe = policy?.SanitizeScalar(textValue) ?? textValue;
                if (safe != textValue) { status = "redacted"; value = null; }
                else if (safe.Length > 512) { status = "truncated"; value = safe[..512]; reasons.Add("text_truncated"); }
            }
            return new(attribute, type, status, value is null ? null : JsonSerializer.SerializeToElement(value), "avalonia_public_" + attribute);
        }
    }

    private static string QueryIdentity(object node, bool includeValues = true) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        generation = CreateObjectGeneration(node), name = includeValues ? GetName(node) : null,
        automationId = includeValues ? GetAutomationId(node) : null, text = includeValues ? GetText(node) : null,
        data = node is StyledElement { DataContext: { } data } ? CreateObjectGeneration(data) : null,
        label = node is Control control && AutomationProperties.GetLabeledBy(control) is { } label ? CreateObjectGeneration(label) : null,
        labelTarget = node is Label { Target: { } labelTarget } ? CreateObjectGeneration(labelTarget) : null
    })));

    private CoreError? RecheckQueryTarget(TopLevel top, RuntimeTargetContext? target)
    {
        if (target?.Selection is null) return null;
        var current = ResolveMutationTarget(top, target);
        return current.Success ? null : new(BridgeErrorCodes.InvalidInputRequest,
            "The selected relationship target changed during preparation. Observe current state before retrying; preparation callbacks may have run.",
            new Dictionary<string, string> { ["dispatched"] = "unknown", ["nextAction"] = "Resolve the relationship selector again and inspect any preparation effects." });
    }

    private static SemanticWorkflowSelector CopyQuerySelector(SemanticWorkflowSelector selector, int maxDepth) => new(
        selector.NodeId, selector.TreeKind, selector.AutomationId, selector.Text, selector.Name, selector.NodeType, selector.Role,
        selector.BindingPath, selector.CommandName, maxDepth, selector.Visible, selector.Enabled, selector.Rendered, selector.Actionable, selector.Relationships);

    private sealed class QueryNode(object node, QueryNode? parent, int depth, bool excluded, string identity)
    {
        public object Node { get; } = node;
        public QueryNode? Parent { get; } = parent;
        public int Depth { get; } = depth;
        public bool Excluded { get; } = excluded;
        public string Identity { get; } = identity;
        public object[]? Children { get; set; }
        public TreeNodeSummary? Summary { get; set; }
    }
}
