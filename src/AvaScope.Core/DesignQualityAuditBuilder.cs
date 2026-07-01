using System.Globalization;
using System.Text.RegularExpressions;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class DesignQualityAuditBuilder
{
    private const double AlignmentTolerance = 1.5;
    private const double RepeatedHeightTolerance = 2.0;
    private const double GapTolerance = 2.0;
    private const double ThinLineMaximum = 1.5;
    private const double LowContrastThreshold = 3.0;

    private readonly TimeProvider _timeProvider;

    public DesignQualityAuditBuilder()
        : this(TimeProvider.System)
    {
    }

    public DesignQualityAuditBuilder(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public CoreResult<DesignQualityAuditResponse> Create(TreeResponse tree, DesignQualityAuditRequest request)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(request);

        var nodes = Flatten(tree.Root).ToArray();
        var diagnostics = new List<ProtocolError>();
        if (nodes.Length == 0)
        {
            return CoreResult<DesignQualityAuditResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Design-quality audit requires a non-empty tree."));
        }

        var scopeRoot = ResolveScopeRoot(nodes, request);
        if (scopeRoot is null)
        {
            return CoreResult<DesignQualityAuditResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Design-quality audit scope did not match any node.",
                CreateScopeDetails(request)));
        }

        var scopedNodeIds = CollectScopeNodeIds(scopeRoot);
        var scopedNodes = nodes
            .Where(node => scopedNodeIds.Contains(node.Node.NodeId))
            .Where(node => request.ScopeRegion is null || Intersects(node.Node.Bounds, request.ScopeRegion))
            .Where(node => MatchesChangeFilter(node, request))
            .ToArray();

        if (request.HasChangeFilter && scopedNodes.Length == 0)
        {
            diagnostics.Add(new ProtocolError(
                "design_quality_changed_scope_empty",
                "The changed-node/source filter matched no nodes inside the selected audit scope.",
                new Dictionary<string, string>
                {
                    ["changedNodeIds"] = request.ChangedNodeIds.Count.ToString(CultureInfo.InvariantCulture),
                    ["changedSourcePaths"] = request.ChangedSourcePaths.Count.ToString(CultureInfo.InvariantCulture)
                }));
        }

        var excludedNodes = CreateExcludedNodeMap(scopedNodes, request);
        var candidateFindings = CreateCandidateFindings(scopedNodes, request).ToArray();
        var activeFindings = new List<DesignQualityFinding>();
        var ignoredFindings = new List<DesignQualityFinding>();

        foreach (var finding in candidateFindings)
        {
            var ignoreReason = FindIgnoreReason(finding, excludedNodes, request.Suppressions);
            if (ignoreReason is not null)
            {
                ignoredFindings.Add(finding.MarkIgnored(ignoreReason));
                continue;
            }

            activeFindings.Add(finding);
        }

        var activeLimit = Math.Min(request.MaxFindings, DesignQualityAuditResponse.MaximumFindings);
        var ignoredLimit = Math.Min(request.MaxFindings, DesignQualityAuditResponse.MaximumFindings);
        var categoryCounts = activeFindings
            .GroupBy(static finding => finding.Category, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        var truncated = activeFindings.Count > activeLimit || ignoredFindings.Count > ignoredLimit;
        var status = activeFindings.Count > 0
            ? "issues_found"
            : ignoredFindings.Count > 0
                ? "clean_with_ignored_findings"
                : "clean";
        var summary = new DesignQualityAuditSummary(
            nodes.Length,
            scopedNodes.Length,
            scopedNodes.Length - excludedNodes.Count,
            nodes.Length - scopedNodes.Length,
            excludedNodes.Count,
            activeFindings.Count,
            ignoredFindings.Count,
            request.Suppressions.Count,
            status,
            CreateScopeStatus(request),
            truncated,
            categoryCounts);

        var metadata = new Dictionary<string, string>
        {
            ["scopeNodeId"] = scopeRoot.Node.NodeId,
            ["scopeNodeType"] = scopeRoot.Node.NodeType,
            ["scopeName"] = scopeRoot.Node.Name ?? "not_available",
            ["scopeAutomationId"] = scopeRoot.Node.AutomationId ?? "not_available",
            ["sourcePath"] = SourcePath(scopeRoot.Node) ?? "not_available",
            ["changeFilter"] = request.HasChangeFilter ? "enabled" : "disabled",
            ["auditKinds"] = request.AuditKinds.Count == 0 ? "all" : string.Join(",", request.AuditKinds),
            ["provenance"] = "runtime_tree_bounds_metadata"
        };

        return CoreResult<DesignQualityAuditResponse>.Ok(new DesignQualityAuditResponse(
            request.RequestId,
            tree.SessionId,
            tree.TopLevelId,
            tree.TreeKind,
            _timeProvider.GetUtcNow(),
            summary,
            scopeRoot.Node.Target ?? new RuntimeTargetContext(tree.SessionId, tree.TopLevelId, tree.TreeKind, scopeRoot.Node.NodeId),
            activeFindings.Take(activeLimit).ToArray(),
            ignoredFindings.Take(ignoredLimit).ToArray(),
            diagnostics,
            metadata));
    }

    private static IEnumerable<DesignQualityFinding> CreateCandidateFindings(
        IReadOnlyList<AuditNode> scopedNodes,
        DesignQualityAuditRequest request)
    {
        var sequence = new FindingSequence();
        var scopedIds = scopedNodes.Select(static node => node.Node.NodeId).ToHashSet(StringComparer.Ordinal);

        if (AuditEnabled(request, "alignment"))
        {
            foreach (var finding in FindIconCenterMismatches(scopedNodes, scopedIds, sequence))
            {
                yield return finding;
            }
        }

        if (AuditEnabled(request, "spacing"))
        {
            foreach (var finding in FindRepeatedHeightIssues(scopedNodes, scopedIds, sequence))
            {
                yield return finding;
            }

            foreach (var finding in FindSpacingGapIssues(scopedNodes, scopedIds, sequence))
            {
                yield return finding;
            }
        }

        if (AuditEnabled(request, "contrast"))
        {
            foreach (var finding in FindLowContrastIndicators(scopedNodes, sequence))
            {
                yield return finding;
            }
        }

        if (AuditEnabled(request, "seam") || AuditEnabled(request, "surface"))
        {
            foreach (var finding in FindUnintendedSeams(scopedNodes, sequence))
            {
                yield return finding;
            }
        }

        if (AuditEnabled(request, "wrapping") || AuditEnabled(request, "density"))
        {
            foreach (var finding in FindWrappedDensityIssues(scopedNodes, scopedIds, sequence))
            {
                yield return finding;
            }
        }

        if (AuditEnabled(request, "radius") || AuditEnabled(request, "layering"))
        {
            foreach (var finding in FindCornerRadiusMismatches(scopedNodes, scopedIds, sequence))
            {
                yield return finding;
            }
        }
    }

    private static IEnumerable<DesignQualityFinding> FindIconCenterMismatches(
        IReadOnlyList<AuditNode> nodes,
        HashSet<string> scopedIds,
        FindingSequence sequence)
    {
        foreach (var node in nodes)
        {
            if (!IsIconNode(node.Node) || node.Parent is null || !scopedIds.Contains(node.Parent.Node.NodeId))
            {
                continue;
            }

            var childBounds = node.Node.Bounds;
            var parentBounds = node.Parent.Node.Bounds;
            if (childBounds is null || parentBounds is null)
            {
                continue;
            }

            var deltaX = CenterX(childBounds) - CenterX(parentBounds);
            var deltaY = CenterY(childBounds) - CenterY(parentBounds);
            var parentChildCount = node.Parent.Node.Children.Count;
            var singleVisualIcon = parentChildCount <= 1;
            if (Math.Abs(deltaY) <= AlignmentTolerance
                && (!singleVisualIcon || Math.Abs(deltaX) <= AlignmentTolerance))
            {
                continue;
            }

            yield return CreateFinding(
                sequence.Next(),
                "alignment",
                "warning",
                "design.alignment.icon_center_mismatch",
                "Icon center does not align with its immediate parent visual center.",
                node,
                "Align icon bounds with the parent center or adjust padding/template slot alignment.",
                relatedNodeIds: [node.Parent.Node.NodeId],
                details: new Dictionary<string, string>
                {
                    ["parentNodeId"] = node.Parent.Node.NodeId,
                    ["centerDeltaX"] = FormatDouble(deltaX),
                    ["centerDeltaY"] = FormatDouble(deltaY),
                    ["tolerance"] = FormatDouble(AlignmentTolerance),
                    ["singleVisualIcon"] = singleVisualIcon.ToString(CultureInfo.InvariantCulture)
                });
        }
    }

    private static IEnumerable<DesignQualityFinding> FindRepeatedHeightIssues(
        IReadOnlyList<AuditNode> nodes,
        HashSet<string> scopedIds,
        FindingSequence sequence)
    {
        foreach (var parent in nodes)
        {
            var children = parent.Node.Children
                .Where(child => scopedIds.Contains(child.NodeId))
                .Where(static child => child.Bounds is not null && child.Bounds.Height > 0)
                .GroupBy(static child => PatternName(child), StringComparer.Ordinal)
                .Where(static group => group.Count() >= 3);

            foreach (var group in children)
            {
                var heights = group.Select(static child => child.Bounds!.Height).ToArray();
                var min = heights.Min();
                var max = heights.Max();
                if (max - min <= RepeatedHeightTolerance)
                {
                    continue;
                }

                yield return CreateFinding(
                    sequence.Next(),
                    "spacing",
                    "warning",
                    "design.spacing.repeated_item_height_inconsistent",
                    "Repeated sibling items have inconsistent rendered heights.",
                    parent,
                    "Normalize row/card height, padding, or text wrapping for this repeated item pattern.",
                    relatedNodeIds: group.Select(static child => child.NodeId).ToArray(),
                    details: new Dictionary<string, string>
                    {
                        ["pattern"] = group.Key,
                        ["minimumHeight"] = FormatDouble(min),
                        ["maximumHeight"] = FormatDouble(max),
                        ["delta"] = FormatDouble(max - min),
                        ["tolerance"] = FormatDouble(RepeatedHeightTolerance)
                    });
            }
        }
    }

    private static IEnumerable<DesignQualityFinding> FindSpacingGapIssues(
        IReadOnlyList<AuditNode> nodes,
        HashSet<string> scopedIds,
        FindingSequence sequence)
    {
        foreach (var parent in nodes)
        {
            var children = parent.Node.Children
                .Where(child => scopedIds.Contains(child.NodeId))
                .Where(static child => child.Bounds is not null && child.Bounds.Width > 0 && child.Bounds.Height > 0)
                .ToArray();
            if (children.Length < 4)
            {
                continue;
            }

            foreach (var gapFinding in CreateGapFindings(parent, children, sequence))
            {
                yield return gapFinding;
            }
        }
    }

    private static IEnumerable<DesignQualityFinding> CreateGapFindings(
        AuditNode parent,
        IReadOnlyList<TreeNodeSummary> children,
        FindingSequence sequence)
    {
        var vertical = children
            .OrderBy(static child => child.Bounds!.Y)
            .ThenBy(static child => child.Bounds!.X)
            .ToArray();
        var verticalGaps = AdjacentGaps(vertical, verticalAxis: true).ToArray();
        if (verticalGaps.Length >= 3 && verticalGaps.Max() - verticalGaps.Min() > GapTolerance)
        {
            yield return CreateFinding(
                sequence.Next(),
                "spacing",
                "info",
                "design.spacing.inconsistent_gaps",
                "Sibling vertical gaps vary more than the design-quality tolerance.",
                parent,
                "Use a consistent panel spacing token or normalize margins for repeated children.",
                relatedNodeIds: vertical.Select(static child => child.NodeId).ToArray(),
                details: new Dictionary<string, string>
                {
                    ["axis"] = "vertical",
                    ["minimumGap"] = FormatDouble(verticalGaps.Min()),
                    ["maximumGap"] = FormatDouble(verticalGaps.Max()),
                    ["tolerance"] = FormatDouble(GapTolerance)
                });
        }

        var horizontal = children
            .OrderBy(static child => child.Bounds!.X)
            .ThenBy(static child => child.Bounds!.Y)
            .ToArray();
        var horizontalGaps = AdjacentGaps(horizontal, verticalAxis: false).ToArray();
        if (horizontalGaps.Length >= 3 && horizontalGaps.Max() - horizontalGaps.Min() > GapTolerance)
        {
            yield return CreateFinding(
                sequence.Next(),
                "spacing",
                "info",
                "design.spacing.inconsistent_gaps",
                "Sibling horizontal gaps vary more than the design-quality tolerance.",
                parent,
                "Use a consistent panel spacing token or normalize margins for repeated children.",
                relatedNodeIds: horizontal.Select(static child => child.NodeId).ToArray(),
                details: new Dictionary<string, string>
                {
                    ["axis"] = "horizontal",
                    ["minimumGap"] = FormatDouble(horizontalGaps.Min()),
                    ["maximumGap"] = FormatDouble(horizontalGaps.Max()),
                    ["tolerance"] = FormatDouble(GapTolerance)
                });
        }
    }

    private static IEnumerable<DesignQualityFinding> FindLowContrastIndicators(IReadOnlyList<AuditNode> nodes, FindingSequence sequence)
    {
        foreach (var node in nodes)
        {
            if (!IsIndicatorLike(node.Node) && !IsThinLine(node.Node.Bounds))
            {
                continue;
            }

            var foreground = FirstColor(node.Node, "Foreground", "BorderBrush", "Background");
            var background = NearestBackground(node.Parent);
            if (foreground is null || background is null)
            {
                continue;
            }

            var contrast = ContrastRatio(foreground.Value, background.Value);
            if (contrast >= LowContrastThreshold)
            {
                continue;
            }

            yield return CreateFinding(
                sequence.Next(),
                "contrast",
                "warning",
                "design.contrast.low_contrast_indicator",
                "Indicator, icon, badge, separator, or subtle border contrast is below the audit threshold.",
                node,
                "Increase semantic foreground/border contrast against the surrounding surface or document an intentional suppression.",
                details: new Dictionary<string, string>
                {
                    ["foreground"] = foreground.Value.ToHex(),
                    ["background"] = background.Value.ToHex(),
                    ["contrastRatio"] = FormatDouble(contrast),
                    ["minimumRatio"] = FormatDouble(LowContrastThreshold)
                });
        }
    }

    private static IEnumerable<DesignQualityFinding> FindUnintendedSeams(IReadOnlyList<AuditNode> nodes, FindingSequence sequence)
    {
        foreach (var node in nodes)
        {
            if (!IsThinLine(node.Node.Bounds) || IsIntentionalThinNode(node.Node))
            {
                continue;
            }

            yield return CreateFinding(
                sequence.Next(),
                "surface",
                "warning",
                "design.surface.unintended_1px_seam",
                "A thin 1px-style rendered line appears in a surface boundary without an intentional separator signal.",
                node,
                "Remove the stray border/overlap, align adjacent surfaces, or suppress this finding if the line is an intentional separator.",
                details: new Dictionary<string, string>
                {
                    ["width"] = FormatDouble(node.Node.Bounds!.Width),
                    ["height"] = FormatDouble(node.Node.Bounds.Height),
                    ["thinLineMaximum"] = FormatDouble(ThinLineMaximum)
                });
        }
    }

    private static IEnumerable<DesignQualityFinding> FindWrappedDensityIssues(
        IReadOnlyList<AuditNode> nodes,
        HashSet<string> scopedIds,
        FindingSequence sequence)
    {
        foreach (var parent in nodes)
        {
            if (!IsWrapSensitiveContainer(parent.Node))
            {
                continue;
            }

            var children = parent.Node.Children
                .Where(child => scopedIds.Contains(child.NodeId))
                .Where(static child => child.Bounds is not null)
                .ToArray();
            if (children.Length < 3)
            {
                continue;
            }

            var rowCount = CountVisualRows(children);
            if (rowCount <= 1)
            {
                continue;
            }

            yield return CreateFinding(
                sequence.Next(),
                "density",
                "warning",
                "design.density.toolbar_wrapped",
                "Toolbar, selector, card group, or expander content appears to wrap into multiple visual rows.",
                parent,
                "Review responsive density rules, overflow behavior, or scoped viewport size so this control does not wrap unexpectedly.",
                relatedNodeIds: children.Select(static child => child.NodeId).ToArray(),
                details: new Dictionary<string, string>
                {
                    ["rowCount"] = rowCount.ToString(CultureInfo.InvariantCulture),
                    ["childCount"] = children.Length.ToString(CultureInfo.InvariantCulture)
                });
        }
    }

    private static IEnumerable<DesignQualityFinding> FindCornerRadiusMismatches(
        IReadOnlyList<AuditNode> nodes,
        HashSet<string> scopedIds,
        FindingSequence sequence)
    {
        foreach (var parent in nodes)
        {
            var surfaceChildren = parent.Node.Children
                .Where(child => scopedIds.Contains(child.NodeId))
                .Select(child => (Node: child, Radius: TryGetRadius(child)))
                .Where(static item => item.Radius is not null)
                .ToArray();
            if (surfaceChildren.Length < 2)
            {
                continue;
            }

            var min = surfaceChildren.Min(static item => item.Radius!.Value);
            var max = surfaceChildren.Max(static item => item.Radius!.Value);
            if (max - min <= 2.0)
            {
                continue;
            }

            yield return CreateFinding(
                sequence.Next(),
                "layering",
                "info",
                "design.layering.corner_radius_mismatch",
                "Adjacent sibling surfaces use noticeably different corner-radius values.",
                parent,
                "Use the same radius token for connected sibling surfaces, or separate them visually as distinct layers.",
                relatedNodeIds: surfaceChildren.Select(static item => item.Node.NodeId).ToArray(),
                details: new Dictionary<string, string>
                {
                    ["minimumRadius"] = FormatDouble(min),
                    ["maximumRadius"] = FormatDouble(max)
                });
        }
    }

    private static DesignQualityFinding CreateFinding(
        int sequence,
        string category,
        string severity,
        string code,
        string message,
        AuditNode node,
        string suggestedAction,
        IReadOnlyList<string>? relatedNodeIds = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        return new DesignQualityFinding(
            $"design-quality:{sequence.ToString(CultureInfo.InvariantCulture)}",
            category,
            severity,
            code,
            message,
            "runtime_tree_bounds_metadata",
            node.Node.Target ?? CreateFallbackTarget(node.Node),
            suggestedAction,
            node.Node.NodeId,
            node.Node.NodeType,
            node.Node.Name,
            node.Node.AutomationId,
            SourcePath(node.Node),
            node.Node.Bounds,
            relatedNodeIds,
            details);
    }

    private static RuntimeTargetContext CreateFallbackTarget(TreeNodeSummary node)
    {
        return new RuntimeTargetContext(new SessionId("unknown"), "unknown", TreeKinds.Visual, node.NodeId);
    }

    private static AuditNode? ResolveScopeRoot(IReadOnlyList<AuditNode> nodes, DesignQualityAuditRequest request)
    {
        if (!request.HasExplicitScope)
        {
            return nodes[0];
        }

        return nodes.FirstOrDefault(node =>
            MatchesOptional(request.ScopeNodeId, node.Node.NodeId, StringComparison.Ordinal)
            && MatchesOptional(request.ScopeName, node.Node.Name, StringComparison.Ordinal)
            && MatchesOptional(request.ScopeAutomationId, node.Node.AutomationId, StringComparison.Ordinal)
            && MatchesOptionalPath(request.ScopeSourcePath, SourcePath(node.Node))
            && (request.ScopeRegion is null || Intersects(node.Node.Bounds, request.ScopeRegion)));
    }

    private static IReadOnlyDictionary<string, string> CreateScopeDetails(DesignQualityAuditRequest request)
    {
        var details = new Dictionary<string, string>
        {
            ["nextAction"] = "Refresh visual-tree/logical-tree or relax scope filters, then retry the design-quality audit."
        };
        CopyDetail(details, "scopeNodeId", request.ScopeNodeId);
        CopyDetail(details, "scopeName", request.ScopeName);
        CopyDetail(details, "scopeAutomationId", request.ScopeAutomationId);
        CopyDetail(details, "scopeSourcePath", request.ScopeSourcePath);
        if (request.ScopeRegion is not null)
        {
            details["scopeRegion"] = $"{request.ScopeRegion.X},{request.ScopeRegion.Y},{request.ScopeRegion.Width}x{request.ScopeRegion.Height}";
        }

        return details;
    }

    private static HashSet<string> CollectScopeNodeIds(AuditNode root)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<AuditNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            ids.Add(current.Node.NodeId);
            foreach (var child in current.Children)
            {
                stack.Push(child);
            }
        }

        return ids;
    }

    private static Dictionary<string, string> CreateExcludedNodeMap(
        IReadOnlyList<AuditNode> scopedNodes,
        DesignQualityAuditRequest request)
    {
        var scoped = scopedNodes.ToDictionary(static node => node.Node.NodeId, StringComparer.Ordinal);
        var excluded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in scopedNodes.OrderBy(static node => node.Depth))
        {
            if (node.Parent is not null
                && excluded.TryGetValue(node.Parent.Node.NodeId, out var parentReason))
            {
                excluded[node.Node.NodeId] = $"excluded_by_ancestor:{node.Parent.Node.NodeId}:{parentReason}";
                continue;
            }

            var reason = DirectExclusionReason(node.Node, request);
            if (reason is not null)
            {
                excluded[node.Node.NodeId] = reason;
                foreach (var descendant in node.Children.SelectMany(FlattenChildren))
                {
                    if (scoped.ContainsKey(descendant.Node.NodeId))
                    {
                        excluded[descendant.Node.NodeId] = $"excluded_by_ancestor:{node.Node.NodeId}:{reason}";
                    }
                }
            }
        }

        return excluded;
    }

    private static string? DirectExclusionReason(TreeNodeSummary node, DesignQualityAuditRequest request)
    {
        if (request.ExcludeNodeIds.Contains(node.NodeId, StringComparer.Ordinal))
        {
            return "excludeNodeIds";
        }

        if (MatchesAny(request.ExcludeNames, node.Name, StringComparison.Ordinal))
        {
            return "excludeNames";
        }

        if (MatchesAny(request.ExcludeAutomationIds, node.AutomationId, StringComparison.Ordinal))
        {
            return "excludeAutomationIds";
        }

        if (MatchesAnyType(request.ExcludeTypes, node.NodeType))
        {
            return "excludeTypes";
        }

        if (MatchesAnyPath(request.ExcludeSourcePaths, SourcePath(node)))
        {
            return "excludeSourcePaths";
        }

        return null;
    }

    private static string? FindIgnoreReason(
        DesignQualityFinding finding,
        IReadOnlyDictionary<string, string> excludedNodes,
        IReadOnlyList<DesignQualitySuppression> suppressions)
    {
        if (finding.NodeId is not null && excludedNodes.TryGetValue(finding.NodeId, out var reason))
        {
            return $"excluded:{reason}";
        }

        foreach (var relatedNodeId in finding.RelatedNodeIds)
        {
            if (excludedNodes.TryGetValue(relatedNodeId, out var relatedReason))
            {
                return $"excluded_related_node:{relatedNodeId}:{relatedReason}";
            }
        }

        var suppression = suppressions.FirstOrDefault(rule => MatchesSuppression(rule, finding));
        return suppression is null ? null : $"suppressed:{suppression.Reason}";
    }

    private static bool MatchesSuppression(DesignQualitySuppression rule, DesignQualityFinding finding)
    {
        return MatchesSuppressionText(rule.Code, finding.Code)
            && MatchesOptional(rule.Category, finding.Category, StringComparison.Ordinal)
            && MatchesOptional(rule.NodeId, finding.NodeId, StringComparison.Ordinal)
            && MatchesOptionalType(rule.NodeType, finding.NodeType)
            && MatchesOptional(rule.Name, finding.Name, StringComparison.Ordinal)
            && MatchesOptional(rule.AutomationId, finding.AutomationId, StringComparison.Ordinal)
            && MatchesOptionalPath(rule.SourcePath, finding.SourcePath);
    }

    private static bool MatchesSuppressionText(string? ruleValue, string actual)
    {
        return ruleValue is null
            || string.Equals(ruleValue, "*", StringComparison.Ordinal)
            || string.Equals(ruleValue, actual, StringComparison.Ordinal);
    }

    private static bool MatchesChangeFilter(AuditNode node, DesignQualityAuditRequest request)
    {
        if (!request.HasChangeFilter)
        {
            return true;
        }

        if (request.ChangedNodeIds.Count == 0 && request.ChangedSourcePaths.Count == 0)
        {
            return false;
        }

        var current = node;
        while (current is not null)
        {
            if (request.ChangedNodeIds.Contains(current.Node.NodeId, StringComparer.Ordinal)
                || MatchesAnyPath(request.ChangedSourcePaths, SourcePath(current.Node)))
            {
                return true;
            }

            current = current.Parent;
        }

        return node.Node.Children.Any(child => TreeNodeOrDescendantMatchesChange(child, request));
    }

    private static bool TreeNodeOrDescendantMatchesChange(TreeNodeSummary node, DesignQualityAuditRequest request)
    {
        if (request.ChangedNodeIds.Contains(node.NodeId, StringComparer.Ordinal)
            || MatchesAnyPath(request.ChangedSourcePaths, SourcePath(node)))
        {
            return true;
        }

        return node.Children.Any(child => TreeNodeOrDescendantMatchesChange(child, request));
    }

    private static IEnumerable<AuditNode> FlattenChildren(AuditNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var descendant in FlattenChildren(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<AuditNode> Flatten(TreeNodeSummary root)
    {
        var rootNode = new AuditNode(root, null, 0);
        var stack = new Stack<AuditNode>();
        stack.Push(rootNode);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            for (var index = current.Node.Children.Count - 1; index >= 0; index--)
            {
                var child = new AuditNode(current.Node.Children[index], current, current.Depth + 1);
                current.Children.Add(child);
                stack.Push(child);
            }
        }
    }

    private static IEnumerable<double> AdjacentGaps(IReadOnlyList<TreeNodeSummary> children, bool verticalAxis)
    {
        for (var index = 1; index < children.Count; index++)
        {
            var previous = children[index - 1].Bounds!;
            var current = children[index].Bounds!;
            var gap = verticalAxis
                ? current.Y - (previous.Y + previous.Height)
                : current.X - (previous.X + previous.Width);
            if (gap >= 0)
            {
                yield return gap;
            }
        }
    }

    private static bool AuditEnabled(DesignQualityAuditRequest request, string auditKind)
    {
        return request.AuditKinds.Count == 0
            || request.AuditKinds.Contains(auditKind, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsIconNode(TreeNodeSummary node)
    {
        var token = NodeToken(node);
        return token.Contains("icon", StringComparison.OrdinalIgnoreCase)
            || ShortTypeName(node.NodeType).EndsWith("PathIcon", StringComparison.Ordinal)
            || ShortTypeName(node.NodeType).EndsWith("Icon", StringComparison.Ordinal);
    }

    private static bool IsIndicatorLike(TreeNodeSummary node)
    {
        var token = NodeToken(node);
        return token.Contains("icon", StringComparison.OrdinalIgnoreCase)
            || token.Contains("indicator", StringComparison.OrdinalIgnoreCase)
            || token.Contains("badge", StringComparison.OrdinalIgnoreCase)
            || token.Contains("separator", StringComparison.OrdinalIgnoreCase)
            || token.Contains("divider", StringComparison.OrdinalIgnoreCase)
            || token.Contains("border", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIntentionalThinNode(TreeNodeSummary node)
    {
        var token = NodeToken(node);
        return token.Contains("separator", StringComparison.OrdinalIgnoreCase)
            || token.Contains("divider", StringComparison.OrdinalIgnoreCase)
            || token.Contains("rule", StringComparison.OrdinalIgnoreCase)
            || token.Contains("focus", StringComparison.OrdinalIgnoreCase)
            || token.Contains("indicator", StringComparison.OrdinalIgnoreCase)
            || token.Contains("caret", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThinLine(NodeBounds? bounds)
    {
        if (bounds is null)
        {
            return false;
        }

        return (bounds.Height > 0 && bounds.Height <= ThinLineMaximum && bounds.Width >= 8)
            || (bounds.Width > 0 && bounds.Width <= ThinLineMaximum && bounds.Height >= 8);
    }

    private static bool IsWrapSensitiveContainer(TreeNodeSummary node)
    {
        var token = NodeToken(node);
        return token.Contains("toolbar", StringComparison.OrdinalIgnoreCase)
            || token.Contains("selector", StringComparison.OrdinalIgnoreCase)
            || token.Contains("segmented", StringComparison.OrdinalIgnoreCase)
            || token.Contains("card", StringComparison.OrdinalIgnoreCase)
            || token.Contains("expander", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountVisualRows(IReadOnlyList<TreeNodeSummary> children)
    {
        var rows = new List<double>();
        foreach (var child in children.OrderBy(static child => child.Bounds!.Y))
        {
            var y = child.Bounds!.Y;
            if (!rows.Any(row => Math.Abs(row - y) <= 2.0))
            {
                rows.Add(y);
            }
        }

        return rows.Count;
    }

    private static AuditColor? FirstColor(TreeNodeSummary node, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = node.SourceMap?.PropertyOrigins.FirstOrDefault(origin =>
                string.Equals(origin.PropertyName, propertyName, StringComparison.Ordinal))?.Value;
            if (TryParseColor(value, out var color))
            {
                return color;
            }
        }

        return null;
    }

    private static AuditColor? NearestBackground(AuditNode? node)
    {
        var current = node;
        while (current is not null)
        {
            var color = FirstColor(current.Node, "Background");
            if (color is not null)
            {
                return color;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool TryParseColor(string? value, out AuditColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = Regex.Match(value, "#(?<hex>[0-9a-fA-F]{6}|[0-9a-fA-F]{8})", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        var hex = match.Groups["hex"].Value;
        var offset = hex.Length == 8 ? 2 : 0;
        color = new AuditColor(
            Convert.ToInt32(hex.Substring(offset, 2), 16),
            Convert.ToInt32(hex.Substring(offset + 2, 2), 16),
            Convert.ToInt32(hex.Substring(offset + 4, 2), 16));
        return true;
    }

    private static double ContrastRatio(AuditColor foreground, AuditColor background)
    {
        var foregroundLuminance = RelativeLuminance(foreground);
        var backgroundLuminance = RelativeLuminance(background);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(AuditColor color)
    {
        return 0.2126 * Linear(color.Red) + 0.7152 * Linear(color.Green) + 0.0722 * Linear(color.Blue);
    }

    private static double Linear(int channel)
    {
        var value = channel / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static double? TryGetRadius(TreeNodeSummary node)
    {
        var value = node.SourceMap?.PropertyOrigins.FirstOrDefault(static origin =>
            string.Equals(origin.PropertyName, "CornerRadius", StringComparison.Ordinal))?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(value, @"-?\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
        if (!match.Success || !double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius))
        {
            return null;
        }

        return radius;
    }

    private static bool Intersects(NodeBounds? bounds, ScreenshotRegion region)
    {
        if (bounds is null)
        {
            return false;
        }

        return bounds.X < region.X + region.Width
            && bounds.X + bounds.Width > region.X
            && bounds.Y < region.Y + region.Height
            && bounds.Y + bounds.Height > region.Y;
    }

    private static double CenterX(NodeBounds bounds) => bounds.X + bounds.Width / 2.0;

    private static double CenterY(NodeBounds bounds) => bounds.Y + bounds.Height / 2.0;

    private static string NodeToken(TreeNodeSummary node)
    {
        return string.Join(
            " ",
            new[] { ShortTypeName(node.NodeType), node.Name, node.AutomationId, node.Text }
                .Concat(node.Classes)
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
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

    private static string? SourcePath(TreeNodeSummary node)
    {
        return node.SourceMap?.FilePath
            ?? node.SourceMap?.PropertyOrigins.FirstOrDefault(static origin => !string.IsNullOrWhiteSpace(origin.SourcePath))?.SourcePath;
    }

    private static bool MatchesOptional(string? expected, string? actual, StringComparison comparison)
    {
        return expected is null || string.Equals(expected, actual, comparison);
    }

    private static bool MatchesOptionalType(string? expected, string? actual)
    {
        return expected is null || (actual is not null && MatchesType(expected, actual));
    }

    private static bool MatchesAny(IReadOnlyList<string> expectedValues, string? actual, StringComparison comparison)
    {
        return actual is not null && expectedValues.Any(expected => string.Equals(expected, actual, comparison));
    }

    private static bool MatchesAnyType(IReadOnlyList<string> expectedValues, string actual)
    {
        return expectedValues.Any(expected => MatchesType(expected, actual));
    }

    private static bool MatchesType(string expected, string actual)
    {
        return string.Equals(expected, actual, StringComparison.Ordinal)
            || string.Equals(expected, ShortTypeName(actual), StringComparison.Ordinal);
    }

    private static bool MatchesOptionalPath(string? expected, string? actual)
    {
        return expected is null || (actual is not null && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesAnyPath(IReadOnlyList<string> expectedValues, string? actual)
    {
        return actual is not null && expectedValues.Any(expected => string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateScopeStatus(DesignQualityAuditRequest request)
    {
        if (request.HasChangeFilter)
        {
            return request.HasExplicitScope ? "scoped_changed_only" : "full_tree_changed_only";
        }

        return request.HasExplicitScope ? "scoped" : "full_tree";
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static void CopyDetail(Dictionary<string, string> details, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            details[key] = value;
        }
    }

    private sealed class AuditNode
    {
        public AuditNode(TreeNodeSummary node, AuditNode? parent, int depth)
        {
            Node = node;
            Parent = parent;
            Depth = depth;
        }

        public TreeNodeSummary Node { get; }

        public AuditNode? Parent { get; }

        public int Depth { get; }

        public List<AuditNode> Children { get; } = [];
    }

    private sealed class FindingSequence
    {
        private int _next = 1;

        public int Next()
        {
            return _next++;
        }
    }

    private readonly record struct AuditColor(int Red, int Green, int Blue)
    {
        public string ToHex()
        {
            return $"#{Red:X2}{Green:X2}{Blue:X2}";
        }
    }
}
