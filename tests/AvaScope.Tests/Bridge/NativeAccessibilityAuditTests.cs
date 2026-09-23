using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class NativeAccessibilityAuditTests
{
    [Fact]
    public async Task UnsupportedNativeBackendRetainsBridgeEvidenceAndEnforcesPrivacyOwnershipAndGenerations()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var button = new Button { Name = "AuditButton", Content = "private-label" }; var window = new Window { Width = 250, Height = 180, Content = button };
                try
                {
                    window.Show(); using var registered = runtime.RegisterTopLevel(window); Dispatcher.UIThread.RunJobs();
                    var id = Assert.Single(await runtime.ListTopLevelsAsync()).Id; var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    var target = (await client.WindowAsync(new(new(runtime.SessionId, id)))).Value!.After!.Target;
                    var root = Path.Combine(Path.GetTempPath(), "avascope-native-a11y");
                    var policy = new RuntimeEvidencePolicy(root, redactedText: ["private-label"]);
                    var result = await client.AuditNativeAccessibilityAsync(new(target, policy: policy));
                    Assert.True(result.Success, result.Error?.Message); Assert.Equal("unsupported", result.Value!.Status);
                    Assert.NotEmpty(result.Value.BridgeNodes); Assert.Empty(result.Value.Native.Nodes);
                    Assert.All(result.Value.Comparisons, item => Assert.Equal("unavailable", item.Mapping));
                    Assert.False(OperationResultMapper.IsSuccessful(result)); Assert.DoesNotContain("private-label", JsonSerializer.Serialize(result));
                    Assert.Equal("inspection_policy_denied", (await runtime.AuditNativeAccessibilityAsync(new(target, policy: new(root, authorizedProcessIds: [int.MaxValue])))).Error!.Code);
                    Assert.Equal("native_accessibility_policy_unavailable", (await client.AuditNativeAccessibilityAsync(new(target, policy: new(root, excludedControlAutomationIds: ["private"])))).Error!.Code);
                    var stale = new RuntimeTargetContext(runtime.SessionId, id, topLevelGeneration: "stale");
                    Assert.Equal("native_accessibility_stale", (await client.AuditNativeAccessibilityAsync(new(stale))).Error!.Code);
                    var foreign = new RuntimeTargetContext(new("another-session"), id, topLevelGeneration: target.TopLevelGeneration);
                    Assert.Equal("inspection_session_mismatch", (await runtime.AuditNativeAccessibilityAsync(new(foreign))).Error!.Code);
                    var node = Assert.Single((await client.FindNodesAsync(runtime.SessionId, id, TreeKinds.Visual, name: "AuditButton")).Value!.Matches).Node.Target!;
                    window.Content = null;
                    Assert.Equal("native_accessibility_stale", (await client.AuditNativeAccessibilityAsync(new(target, expectations: [new(node)]))).Error!.Code);
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }
}
