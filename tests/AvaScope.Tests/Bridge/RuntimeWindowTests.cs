using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeWindowTests
{
    [Fact]
    public async Task HeadlessInspectionReportsItsLimitsAndNativeChangesAreNotPretended()
    {
        await WithWindow(async (runtime, window, target, client) =>
        {
            var observed = Snapshot(await client.WindowAsync(new(target)));
            Assert.Equal("headless", observed.Backend.Backend); Assert.Equal("unsupported", observed.NativeState.State);
            Assert.Equal("inspect", Assert.Single(observed.AvailableActions)); Assert.Equal("backend_desktop_units_unverified", observed.DesktopUnits);
            var original = window.ClientSize;
            var denied = Failed(await client.WindowAsync(new(observed.Target, "resize", observed.Revision, clientSize: new(200, 160))), "window_action_unsupported");
            Assert.Equal(0, denied.Value!.DispatchedOperations); Assert.False(denied.Value.Provenance!.Dispatched);
            Assert.Equal(original, window.ClientSize);
        });
    }

    [Fact]
    public async Task GenerationRevisionAndSessionChecksRejectStaleOrUnrelatedWindows()
    {
        await WithWindow(async (runtime, window, target, client) =>
        {
            var initial = Snapshot(await client.WindowAsync(new(target)));
            window.Title = "Another document";
            Failed(await client.WindowAsync(new(initial.Target, "restore", initial.Revision)), "window_stale");
            var wrong = new RuntimeTargetContext(runtime.SessionId, target.TopLevelId, topLevelGeneration: "old-generation");
            Failed(await client.WindowAsync(new(wrong)), "window_stale");
            Failed(await runtime.WindowAsync(new(new(new("another-session"), target.TopLevelId))), "window_session_mismatch");
            Failed(await client.WindowAsync(new(new(runtime.SessionId, "unrelated-top-level"))), "window_unavailable");
            var fresh = Snapshot(await client.WindowAsync(new(target)));
            window.Close();
            Failed(await client.WindowAsync(new(fresh.Target, "restore", fresh.Revision)), "window_unavailable");
        });
    }

    [Fact]
    public async Task RegisteredAndUnregisteredModalChildrenBlockTheirOwner()
    {
        await WithWindow(async (runtime, window, target, client) =>
        {
            var dialog = new Window { Title = "Confirm", Width = 180, Height = 100 };
            var task = dialog.ShowDialog(window);
            try
            {
                Dispatcher.UIThread.RunJobs();
                var without = Snapshot(await client.WindowAsync(new(target)));
                Failed(await client.WindowAsync(new(without.Target, "restore", without.Revision)), "window_modal_blocked");
                using var registered = runtime.RegisterTopLevel(dialog);
                var observed = Snapshot(await client.WindowAsync(new(target)));
                Assert.NotNull(observed.ModalBlocker);
                var child = Snapshot(await client.WindowAsync(new(observed.ModalBlocker!)));
                Assert.True(child.IsDialog); Assert.Equal(target.TopLevelId, child.Owner!.TopLevelId);
                Failed(await client.WindowAsync(new(observed.Target, "activate", observed.Revision)), "window_modal_blocked");
            }
            finally { dialog.Close(); await task; }
        });
    }

    [Fact]
    public async Task EvidencePolicyProtectsWindowMetadataAndExplicitActionPermissions()
    {
        await WithWindow(async (runtime, window, target, client) =>
        {
            window.Title = "private-title";
            var policy = new RuntimeEvidencePolicy(Path.GetTempPath(), redactedText: ["private-title"], authorizedSessionIds: [runtime.SessionId.Value]);
            var result = await client.WindowAsync(new(target, policy: policy));
            var snapshot = Snapshot(result); Assert.DoesNotContain("private-title", JsonSerializer.Serialize(result));
            Failed(await client.WindowAsync(new(snapshot.Target, "restore", snapshot.Revision, policy: policy)), "window_policy_denied");
            AutomationProperties.SetAutomationId(window, "private-window");
            var excluded = new RuntimeEvidencePolicy(Path.GetTempPath(), excludedControlAutomationIds: ["private-window"]);
            var hidden = Failed(await client.WindowAsync(new(target, policy: excluded)), "window_excluded");
            Assert.Null(hidden.Value!.Before); Assert.Null(hidden.Value.After);
            Assert.Throws<ArgumentException>(() => new RuntimeEvidencePolicy(Path.GetTempPath(), allowedWindowActions: ["close"]));
        });
    }

    [Fact]
    public void PlacementUsesMonitorLocalDipOffsetsAndPreservesNegativeDesktopCoordinates()
    {
        var left = new RuntimeMonitorInfo("left", "Left", new(-2560, -200, 2560, 1440), new(-2560, -160, 2560, 1400),
            "physical_desktop_pixels", 2, new(1280, 720), new(-2560, -200, 2560, 1440), false);
        var right = new RuntimeMonitorInfo("right", "Right", new(0, 0, 1920, 1080), new(0, 30, 1920, 1050),
            "physical_desktop_pixels", 1.25, new(1536, 864), new(0, 0, 1920, 1080), true);
        var resolved = RuntimeWindowGeometry.ResolvePosition(new(100, 50, "monitor_dip", "left"), [left, right]);
        Assert.True(resolved.Success); Assert.Equal(-2360, resolved.Value!.X); Assert.Equal(-60, resolved.Value.Y);
        resolved = RuntimeWindowGeometry.ResolvePosition(new(100, 40, "monitor_dip", "right"), [left, right]);
        Assert.Equal(125, resolved.Value!.X); Assert.Equal(80, resolved.Value.Y);
        Assert.True(RuntimeWindowGeometry.ResolvePosition(new(-2400, -100), [left, right]).Success);
        Assert.Equal("window_position_offscreen", RuntimeWindowGeometry.ResolvePosition(new(-9000, 0), [left, right]).Error!.Code);
        Assert.Equal("window_monitor_stale", RuntimeWindowGeometry.ResolvePosition(new(0, 0, "monitor_dip", "removed"), [left, right]).Error!.Code);
        var retina = left with { DesktopUnits = "cocoa_desktop_points", DesktopScale = 1, PhysicalPixelBounds = null };
        resolved = RuntimeWindowGeometry.ResolvePosition(new(100, 50, "monitor_dip", "left"), [retina]);
        Assert.Equal(-2460, resolved.Value!.X); Assert.Equal(-110, resolved.Value.Y);
    }

    [Fact]
    public void ContractsRequireExplicitPinnedIntentAndPartialOutcomesKeepFailureEvidence()
    {
        var target = new RuntimeTargetContext(new("session"), "window", topLevelGeneration: "generation");
        var revision = new string('a', 64);
        Assert.Throws<ArgumentException>(() => new RuntimeWindowRequest(target, "restore"));
        Assert.Throws<ArgumentException>(() => new RuntimeWindowRequest(target, "close", revision));
        Assert.Throws<ArgumentException>(() => new RuntimeWindowRequest(target, "move", revision));
        Assert.Throws<ArgumentException>(() => new RuntimeWindowRequest(target, "resize", revision, clientSize: new(double.NaN, 200)));
        Assert.Throws<ArgumentException>(() => new RuntimeWindowPosition(1.2, 2));
        Assert.Throws<ArgumentException>(() => new RuntimeWindowPosition(1, 2, "monitor_dip"));
        var request = new RuntimeWindowRequest(target, "restore", revision);
        Assert.Equal(request.Action, JsonSerializer.Deserialize<RuntimeWindowRequest>(JsonSerializer.Serialize(request))!.Action);
        Assert.True(BridgeIpcMethods.RequiresControl(new("change", BridgeIpcMethods.Window, window: request)));
        Assert.False(BridgeIpcMethods.RequiresControl(new("read", BridgeIpcMethods.Window, window: new(target))));
        var partial = OperationResultMapper.ToToolResult(CoreResult<RuntimeWindowResponse>.Ok(new("partial", "resize", null, null, 1, null,
            "unconfirmed", [new("window_manager_refused_or_unconfirmed", "No confirmation.")])));
        Assert.False(partial.Success); Assert.Equal(1, partial.Value!.DispatchedOperations); Assert.Equal("partial", partial.Error!.Details!["status"]);
    }

    private static RuntimeWindowSnapshot Snapshot(CoreResult<RuntimeWindowResponse> result)
    { Assert.True(OperationResultMapper.IsSuccessful(result), JsonSerializer.Serialize(result)); return result.Value!.After!; }
    private static ToolResult<RuntimeWindowResponse> Failed(CoreResult<RuntimeWindowResponse> result, string code)
    { var tool = OperationResultMapper.ToToolResult(result); Assert.False(tool.Success); Assert.Equal(code, tool.Error!.Code); return tool; }
    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, Window, RuntimeTargetContext, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var window = new Window { Title = "Window management", Width = 300, Height = 200, Content = new TextBlock { Text = "Owned window" } };
                try
                {
                    window.Show(); using var registered = runtime.RegisterTopLevel(window); Dispatcher.UIThread.RunJobs();
                    var top = Assert.Single(await runtime.ListTopLevelsAsync()).Id;
                    await test(runtime, window, new(runtime.SessionId, top), new(Path.GetDirectoryName(runtime.SessionManifestPath)!));
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }
}
