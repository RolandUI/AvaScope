using System.Globalization;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class UiAuditBuilder
{
    private const int DefaultIssueLimit = 100;
    private const int DefaultInventoryLimit = 100;

    private static readonly HashSet<string> ActionableTypeNames = new(StringComparer.Ordinal)
    {
        "Button",
        "ToggleButton",
        "RepeatButton",
        "CheckBox",
        "RadioButton",
        "TextBox",
        "ComboBox",
        "Slider",
        "MenuItem",
        "ListBoxItem",
        "TreeViewItem",
        "TabItem",
        "ToggleSwitch",
        "CalendarDatePicker",
        "NumericUpDown"
    };

    private readonly TimeProvider _timeProvider;

    public UiAuditBuilder()
        : this(TimeProvider.System)
    {
    }

    public UiAuditBuilder(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public CoreResult<UiAuditResponse> Create(
        TreeResponse tree,
        int? maxIssues = null,
        int? maxInventoryItems = null)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var issueLimit = NormalizeLimit(maxIssues, UiAuditResponse.MaximumIssues, "maxIssues");
        if (!issueLimit.Success)
        {
            return CoreResult<UiAuditResponse>.Fail(issueLimit.Error!);
        }

        var inventoryLimit = NormalizeLimit(maxInventoryItems, UiAuditResponse.MaximumInventoryItems, "maxInventoryItems");
        if (!inventoryLimit.Success)
        {
            return CoreResult<UiAuditResponse>.Fail(inventoryLimit.Error!);
        }

        var nodes = Flatten(tree.Root).ToArray();
        var actionableNodes = nodes.Where(static node => IsActionable(node.Node)).ToArray();
        var issues = CreateIssues(actionableNodes).ToArray();
        var inventory = CreateInventory(nodes).ToArray();
        var validationMetadataCount = nodes.Count(static node => node.Node.ValidationState is not null);
        var validationErrorCount = nodes.Count(static node => node.Node.ValidationState?.HasErrors == true);
        var focusKnownCount = actionableNodes.Count(static node =>
            node.Node.AccessibilityState?.Focusable is not null
            || node.Node.AccessibilityState?.IsTabStop is not null);
        var summary = new UiAuditSummary(
            nodes.Length,
            actionableNodes.Length,
            nodes.Count(static node => !string.IsNullOrWhiteSpace(node.Node.AutomationId)),
            nodes.Count(static node => !string.IsNullOrWhiteSpace(AccessibleName(node.Node))),
            validationMetadataCount,
            validationErrorCount,
            nodes.Select(static node => ShortTypeName(node.Node.NodeType)).Distinct(StringComparer.Ordinal).Count(),
            nodes.SelectMany(static node => node.Node.Classes).Distinct(StringComparer.Ordinal).Count(),
            inventory.Count(static item => item.Category == "component_pattern"),
            issues.Length,
            inventory.Length,
            issues.Any(static issue => issue.Category == "accessibility") ? "issues_found" : "available",
            validationMetadataCount == 0 ? "not_available" : validationErrorCount > 0 ? "errors_found" : "clean",
            actionableNodes.Length == 0 ? "not_available" : focusKnownCount == 0 ? "not_available" : focusKnownCount == actionableNodes.Length ? "available" : "partial",
            truncated: issues.Length > issueLimit.Value || inventory.Length > inventoryLimit.Value);

        return CoreResult<UiAuditResponse>.Ok(new UiAuditResponse(
            tree.SessionId,
            tree.TopLevelId,
            tree.TreeKind,
            tree.DepthLimit,
            _timeProvider.GetUtcNow(),
            summary,
            issues.Take(issueLimit.Value).ToArray(),
            inventory.Take(inventoryLimit.Value).ToArray(),
            tree.Target));
    }

    private static CoreResult<int> NormalizeLimit(int? value, int maximum, string optionName)
    {
        if (value is < 1)
        {
            return CoreResult<int>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"{optionName} must be positive."));
        }

        return CoreResult<int>.Ok(Math.Min(value ?? maximum, maximum));
    }

    private static IEnumerable<UiAuditIssue> CreateIssues(IReadOnlyList<NodeWithDepth> actionableNodes)
    {
        var sequence = 1;
        foreach (var item in actionableNodes)
        {
            var node = item.Node;
            var accessibleName = AccessibleName(node);
            if (string.IsNullOrWhiteSpace(accessibleName))
            {
                yield return CreateIssue(
                    sequence++,
                    "accessibility",
                    "warning",
                    "accessibility.missing_accessible_name",
                    "Actionable node has no automation name, text, or stable name metadata.",
                    node,
                    "Add AutomationProperties.Name or visible text for this control before relying on agent or assistive-tool automation.",
                    new Dictionary<string, string>
                    {
                        ["depth"] = item.Depth.ToString(CultureInfo.InvariantCulture),
                        ["signal"] = "automationName,text,name"
                    });
            }

            if (string.IsNullOrWhiteSpace(node.AutomationId))
            {
                yield return CreateIssue(
                    sequence++,
                    "accessibility",
                    "info",
                    "accessibility.missing_automation_id",
                    "Actionable node has no stable AutomationId.",
                    node,
                    "Add AutomationProperties.AutomationId to make this control stable for agents, UI automation, and regression reports.",
                    new Dictionary<string, string>
                    {
                        ["depth"] = item.Depth.ToString(CultureInfo.InvariantCulture),
                        ["signal"] = "automationId"
                    });
            }

            if (node.AccessibilityState?.Focusable == false || node.AccessibilityState?.IsTabStop == false)
            {
                yield return CreateIssue(
                    sequence++,
                    "accessibility",
                    "warning",
                    "accessibility.keyboard_not_focusable",
                    "Actionable node is not keyboard focusable or not in tab navigation.",
                    node,
                    "Verify Focusable, IsEnabled, KeyboardNavigation.IsTabStop, and the containing tab navigation mode.",
                    new Dictionary<string, string>
                    {
                        ["focusable"] = node.AccessibilityState.Focusable?.ToString() ?? "unknown",
                        ["isTabStop"] = node.AccessibilityState.IsTabStop?.ToString() ?? "unknown",
                        ["tabIndex"] = node.AccessibilityState.TabIndex?.ToString(CultureInfo.InvariantCulture) ?? "unknown"
                    });
            }

            if (node.ValidationState?.HasErrors == true)
            {
                yield return CreateIssue(
                    sequence++,
                    "validation",
                    "warning",
                    "validation.errors_present",
                    "Runtime validation errors are currently present on this node.",
                    node,
                    "Inspect the bound viewmodel validation state and error template; fix the underlying validation source before recording a clean audit.",
                    new Dictionary<string, string>
                    {
                        ["errorCount"] = node.ValidationState.ErrorCount?.ToString(CultureInfo.InvariantCulture) ?? "unknown",
                        ["errors"] = string.Join(" | ", node.ValidationState.Errors)
                    });
            }
        }
    }

    private static UiAuditIssue CreateIssue(
        int sequence,
        string category,
        string severity,
        string code,
        string message,
        TreeNodeSummary node,
        string suggestedAction,
        IReadOnlyDictionary<string, string> details)
    {
        return new UiAuditIssue(
            $"ui-audit:{sequence.ToString(CultureInfo.InvariantCulture)}",
            category,
            severity,
            code,
            message,
            IssueProvenance(node),
            node.Target ?? new RuntimeTargetContext(new SessionId("unknown"), "unknown", TreeKinds.Visual, node.NodeId),
            suggestedAction,
            node.NodeId,
            node.NodeType,
            node.Name,
            node.AutomationId,
            details);
    }

    private static IEnumerable<UiInventoryItem> CreateInventory(IReadOnlyList<NodeWithDepth> nodes)
    {
        foreach (var group in nodes.GroupBy(static node => ShortTypeName(node.Node.NodeType)).OrderByDescending(static group => group.Count()).ThenBy(static group => group.Key, StringComparer.Ordinal))
        {
            yield return new UiInventoryItem(
                $"inventory:control:{SanitizeId(group.Key)}",
                "control",
                group.Key,
                group.Count(),
                "runtime_tree",
                sampleTargets: group.Select(static item => item.Node.Target).Where(static target => target is not null).Cast<RuntimeTargetContext>().Take(UiInventoryItem.MaximumSampleTargets).ToArray());
        }

        foreach (var group in nodes.SelectMany(static node => node.Node.Classes.Select(cssClass => (Node: node.Node, Class: cssClass))).GroupBy(static item => item.Class).OrderByDescending(static group => group.Count()).ThenBy(static group => group.Key, StringComparer.Ordinal))
        {
            yield return new UiInventoryItem(
                $"inventory:class:{SanitizeId(group.Key)}",
                "class",
                group.Key,
                group.Count(),
                "runtime_tree_classes",
                sampleTargets: group.Select(static item => item.Node.Target).Where(static target => target is not null).Cast<RuntimeTargetContext>().Take(UiInventoryItem.MaximumSampleTargets).ToArray());
        }

        foreach (var group in nodes.GroupBy(static item => PatternName(item.Node)).Where(static group => group.Count() > 1).OrderByDescending(static group => group.Count()).ThenBy(static group => group.Key, StringComparer.Ordinal))
        {
            yield return new UiInventoryItem(
                $"inventory:component-pattern:{SanitizeId(group.Key)}",
                "component_pattern",
                group.Key,
                group.Count(),
                "runtime_tree_type_and_classes",
                sampleTargets: group.Select(static item => item.Node.Target).Where(static target => target is not null).Cast<RuntimeTargetContext>().Take(UiInventoryItem.MaximumSampleTargets).ToArray());
        }

        foreach (var group in nodes.Where(static item => item.Node.ValidationState is not null).GroupBy(static item => item.Node.ValidationState!.Status).OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            yield return new UiInventoryItem(
                $"inventory:validation:{SanitizeId(group.Key)}",
                "validation_state",
                group.Key,
                group.Count(),
                "avalonia_public_data_validation_errors",
                status: group.Key,
                sampleTargets: group.Select(static item => item.Node.Target).Where(static target => target is not null).Cast<RuntimeTargetContext>().Take(UiInventoryItem.MaximumSampleTargets).ToArray());
        }

        yield return NotAvailableInventory("style", "style_setters", "Runtime style setter inventory requires style provenance that Avalonia public APIs do not expose reliably.");
        yield return NotAvailableInventory("resource", "resource_dictionaries", "Runtime resource dictionary ownership is not available from the bounded tree snapshot.");
        yield return NotAvailableInventory("template", "control_templates", "Template ownership is not available from the bounded tree snapshot.");
        yield return NotAvailableInventory("theme_variant", "theme_variants", "Theme variant inventory is available in preview responses, not in this runtime tree audit.");
    }

    private static UiInventoryItem NotAvailableInventory(string category, string name, string reason)
    {
        return new UiInventoryItem(
            $"inventory:{category}:{name}",
            category,
            name,
            0,
            "not_available",
            "not_available",
            details: new Dictionary<string, string>
            {
                ["reason"] = reason,
                ["nextAction"] = "Use preview diagnostics or source-aware review context when this inventory category is required."
            });
    }

    private static IEnumerable<NodeWithDepth> Flatten(TreeNodeSummary root)
    {
        var stack = new Stack<NodeWithDepth>();
        stack.Push(new NodeWithDepth(root, 0));
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var index = current.Node.Children.Count - 1; index >= 0; index--)
            {
                stack.Push(new NodeWithDepth(current.Node.Children[index], current.Depth + 1));
            }
        }
    }

    private static bool IsActionable(TreeNodeSummary node)
    {
        var shortName = ShortTypeName(node.NodeType);
        return ActionableTypeNames.Contains(shortName)
            || shortName.EndsWith("Button", StringComparison.Ordinal)
            || shortName.EndsWith("TextBox", StringComparison.Ordinal)
            || shortName.EndsWith("MenuItem", StringComparison.Ordinal);
    }

    private static string? AccessibleName(TreeNodeSummary node)
    {
        return FirstNonEmpty(node.AccessibilityState?.AutomationName, node.Text, node.Name);
    }

    private static string IssueProvenance(TreeNodeSummary node)
    {
        return node.AccessibilityState is not null || node.ValidationState is not null
            ? "runtime_tree+public_avalonia_metadata"
            : "runtime_tree";
    }

    private static string PatternName(TreeNodeSummary node)
    {
        var classes = node.Classes.Count == 0 ? "no-class" : string.Join(".", node.Classes.Order(StringComparer.Ordinal));
        return $"{ShortTypeName(node.NodeType)}:{classes}";
    }

    private static string ShortTypeName(string typeName)
    {
        var index = typeName.LastIndexOf('.');
        return index < 0 ? typeName : typeName[(index + 1)..];
    }

    private static string SanitizeId(string value)
    {
        var chars = value
            .Select(static character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        return new string(chars).Trim('-');
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private sealed record NodeWithDepth(TreeNodeSummary Node, int Depth);
}
