using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class NativeAccessibilityComparerTests
{
    private static readonly RuntimeTargetContext Target = new(new("audit-session"), "window", TreeKinds.Visual, "node", topLevelGeneration: "top-generation", nodeGeneration: "node-generation");
    private static readonly NodeBounds Bounds = new(10, 20, 100, 30);
    private static BridgeAccessibilityEvidence Node(string? id = "save", NativeAccessibilityExpectation? expectation = null)
        => new(Target, "Button", id, "Save", "Button", true, true, true, Bounds, expectation);
    private static NativeAccessibilityNode Native(string? id = "save", string? name = "Save", string? role = "push button")
        => new("native-1", null, id, name, role, true, false, Bounds);
    private static NativeAccessibilitySnapshot Snapshot(params NativeAccessibilityNode[] nodes) => new("observed", "test_native", "physical_desktop_pixels", DateTimeOffset.UtcNow, nodes, false, []);

    [Fact]
    public void IdentityAndGeometryAreDistinctAndDuplicateIdentitiesStayAmbiguous()
    {
        var identity = Assert.Single(NativeAccessibilityComparer.Compare([Node()], Snapshot(Native())));
        Assert.Equal("high_identity", identity.Confidence); Assert.Empty(identity.Findings);
        Assert.Equal("candidate_identity_in_partial_sample", Assert.Single(NativeAccessibilityComparer.Compare([Node()], Snapshot(Native()), bridgeTruncated: true)).Confidence);
        Assert.Equal("candidate_identity_in_partial_sample", Assert.Single(NativeAccessibilityComparer.Compare([Node()], Snapshot(Native()) with { Truncated = true })).Confidence);
        var geometry = Assert.Single(NativeAccessibilityComparer.Compare([Node(null)], Snapshot(Native(null))));
        Assert.Equal("probable_geometry", geometry.Confidence);
        Assert.Equal("ambiguous", Assert.Single(NativeAccessibilityComparer.Compare([Node()], Snapshot(Native(), Native() with { Id = "native-2" }))).Mapping);
        Assert.All(NativeAccessibilityComparer.Compare([Node(), Node() with { Target = Target with { } }], Snapshot(Native())), result => Assert.Equal("ambiguous", result.Mapping));
        Assert.True(NativeAccessibilityComparer.CompatibleRole("HeaderItem", "column header"));
        Assert.True(NativeAccessibilityComparer.CompatibleRole("Custom", "unknown"));
        Assert.False(NativeAccessibilityComparer.SameBounds(new(0, 0, 1, 1), new(0, 0, 0, 0)));
    }

    [Fact]
    public void MissingNamesAndExplicitRolesAreFindingsWithoutTreatingEveryDifferenceAsFailure()
    {
        var expected = Node(expectation: new(Target, "Save", "Button"));
        var comparison = Assert.Single(NativeAccessibilityComparer.Compare([expected], Snapshot(Native(name: "", role: "Image"))));
        Assert.Contains("native_name_missing", comparison.Findings); Assert.Contains("accessible_role_differs", comparison.Findings);
        var decorative = Node() with { IsControlElement = false, AutomationId = "decoration", Role = "None", Name = null };
        Assert.Empty(Assert.Single(NativeAccessibilityComparer.Compare([decorative], Snapshot())).Findings);
        Assert.Contains(Assert.Single(NativeAccessibilityComparer.Compare([expected], Snapshot())).Findings, f => f.StartsWith("expected_control_not_mapped"));
        Assert.Empty(Assert.Single(NativeAccessibilityComparer.Compare([expected], Snapshot() with { Truncated = true })).Findings);
        Assert.Empty(Assert.Single(NativeAccessibilityComparer.Compare([expected], Snapshot() with { Status = "partial" })).Findings);
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("unsupported")]
    public void MissingNativeServicesNeverProduceHealthyOrMissingControlClaims(string status)
    {
        var comparison = Assert.Single(NativeAccessibilityComparer.Compare([Node(expectation: new(Target))], Snapshot() with { Status = status }));
        Assert.Equal("unavailable", comparison.Mapping); Assert.Empty(comparison.Findings);
    }
}
