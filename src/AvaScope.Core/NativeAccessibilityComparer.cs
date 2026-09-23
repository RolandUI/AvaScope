using AvaScope.Protocol;

namespace AvaScope.Core;

public static class NativeAccessibilityComparer
{
    public static IReadOnlyList<NativeAccessibilityComparison> Compare(IReadOnlyList<BridgeAccessibilityEvidence> bridge, NativeAccessibilitySnapshot native, bool bridgeTruncated = false)
    {
        if (bridge.Count > 256 || native.Nodes.Count > 256) throw new ArgumentException("Accessibility comparison is limited to 256 nodes per source.");
        return bridge.Select(item =>
        {
            var findings = new List<string>();
            if (native.Status is not ("observed" or "partial")) return Result("unavailable", "none", []);
            var byId = !string.IsNullOrEmpty(item.AutomationId);
            var candidates = byId ? native.Nodes.Where(n => n.AutomationId == item.AutomationId).ToArray() : [];
            var provenance = "automation_id";
            if (candidates.Length == 0)
            {
                byId = false; provenance = "geometry_and_semantics";
                candidates = native.Nodes.Where(n => SameBounds(item.DesktopBounds, n.DesktopBounds)
                    && (item.Name is { Length: > 0 } && item.Name == n.Name || item.Role is not null && CompatibleRole(item.Role, n.Role))).ToArray();
            }
            if (candidates.Length > 1 || byId && bridge.Count(b => b.AutomationId == item.AutomationId) > 1)
                return Result("ambiguous", "none", candidates);
            if (candidates.Length == 0)
            {
                if (item.Expectation is not null && native.Status == "observed" && !native.Truncated && item.Visible)
                    findings.Add("expected_control_not_mapped; review_grouping_virtualization_and_native_children");
                return Result(native.Truncated || native.Status == "partial" ? "unobserved_in_partial_tree" : "not_mapped", "none", []);
            }
            var match = candidates[0];
            var expectedName = item.Expectation?.Name ?? item.Name;
            var expectedRole = item.Expectation?.Role ?? item.Role;
            if (item.Expectation is not null || item.IsControlElement == true)
            {
                if (string.IsNullOrWhiteSpace(match.Name)) findings.Add("native_name_missing");
                else if (expectedName is not null && match.Name != expectedName) findings.Add("accessible_name_differs");
                if (expectedRole is not null && match.Role is not null && !CompatibleRole(expectedRole, match.Role)) findings.Add("accessible_role_differs");
                if (match.Enabled is { } enabled && enabled != item.Enabled) findings.Add("enabled_state_differs; sequential_observations");
            }
            return Result(provenance, byId ? native.Truncated || bridgeTruncated || native.Status == "partial" ? "candidate_identity_in_partial_sample" : "high_identity" : "probable_geometry", candidates);

            NativeAccessibilityComparison Result(string mapping, string confidence, IReadOnlyList<NativeAccessibilityNode> nodes)
                => new(item.Target, mapping, confidence, nodes.Take(16).Select(n => n.Id).ToArray(), findings);
        }).ToArray();
    }

    public static bool SameBounds(NodeBounds? a, NodeBounds? b) => a is not null && b is not null && a.Width > 0 && a.Height > 0 && b.Width > 0 && b.Height > 0
        && Math.Abs(a.X - b.X) <= 2 && Math.Abs(a.Y - b.Y) <= 2 && Math.Abs(a.Width - b.Width) <= 2 && Math.Abs(a.Height - b.Height) <= 2;

    public static bool CompatibleRole(string a, string? b) => b is not null && Roles(a).Intersect(Roles(b), StringComparer.Ordinal).Any();
    private static string[] Roles(string role) => role.ToLowerInvariant().Replace(" ", "").Replace("_", "") switch
    {
        "button" or "pushbutton" or "togglebutton" or "splitbutton" or "thumb" => ["button"],
        "edit" or "entry" or "passwordtext" => ["edit"],
        "text" or "label" or "statictext" => ["text"],
        "frame" or "window" => ["window"],
        "panel" or "pane" or "group" or "none" or "expander" => ["group"],
        "listitem" or "comboboxitem" => ["listitem"],
        "treetable" or "datagrid" => ["datagrid"],
        "tablecell" or "dataitem" => ["dataitem"],
        "pagetab" or "tabitem" => ["tabitem"],
        "pagetablist" or "tab" => ["tab"],
        "spinbutton" or "spinner" => ["spinner"],
        "documentframe" or "document" => ["document"],
        "scrollpane" or "scrollviewer" => ["scrollviewer"],
        "link" or "hyperlink" => ["link"],
        "custom" or "unknown" => ["custom"],
        "headeritem" or "columnheader" => ["headeritem"],
        var value => [value]
    };
}
