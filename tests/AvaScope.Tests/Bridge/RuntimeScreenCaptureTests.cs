using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeScreenCaptureTests
{
    [Fact]
    public async Task PairedCapturePreservesRenderButRefusesDesktopWithoutHostGrant()
    {
        await WithWindow(async (runtime, window, target, client, output) =>
        {
            var result = Value(await client.CaptureScreenAsync(new(target, output, desktopScope: "declared_test_desktop")));
            Assert.Equal("partial", result.Status); Assert.Equal("captured", result.Rendered!.Status);
            Assert.Equal("native_screen_scope_denied", Assert.Single(result.Native!.Diagnostics).Code);
            Assert.Null(result.Native.FilePath); Assert.Null(result.Native.Png); Assert.Null(result.Rendered.Png);
            Assert.Equal("unavailable", result.Comparison!.Status); Assert.False(OperationResultMapper.IsSuccessful(CoreResult<RuntimeScreenCaptureResponse>.Ok(result)));
            using var image = SKBitmap.Decode(result.Rendered.FilePath); Assert.Equal(SKColors.Blue, image.GetPixel(100, 60));
            runtime.SetNativeScreenCaptureScope("declared_test_desktop");
            var unsupported = Value(await client.CaptureScreenAsync(new(target, output, desktopScope: "declared_test_desktop")));
            Assert.Equal("native_screen_unsupported", Assert.Single(unsupported.Native!.Diagnostics).Code);
            Assert.Equal("avalonia_render_target_bitmap", unsupported.Rendered!.Source);
            runtime.SetNativeScreenCaptureScope(null);
            Assert.Null(runtime.NativeScreenCaptureScope);
        });
    }

    [Fact]
    public async Task PolicyMasksBeforeBridgeIpcAndBeforeArtifactsAndRequiresExplicitNativePermission()
    {
        await WithWindow(async (runtime, window, target, client, output) =>
        {
            var policy = new RuntimeEvidencePolicy(output, redactedText: ["sensitive-field"]);
            var request = new RuntimeScreenCaptureRequest(target, output, "rendered", policy: policy);
            var direct = Value(await runtime.CaptureScreenAsync(request));
            Assert.Equal("full_sensitive_mask", direct.Rendered!.Masking);
            using (var image = SKBitmap.Decode(direct.Rendered.Png)) Assert.Equal(SKColors.Black, image.GetPixel(100, 60));
            Assert.False(Directory.Exists(output)); // bridge never writes raw or masked temporary images
            var exported = Value(await client.CaptureScreenAsync(request));
            Assert.Null(exported.Rendered!.Png);
            using (var image = SKBitmap.Decode(exported.Rendered.FilePath)) Assert.Equal(SKColors.Black, image.GetPixel(100, 60));
            runtime.SetNativeScreenCaptureScope("declared_test_desktop");
            var denied = Value(await client.CaptureScreenAsync(new(target, output, "native", "declared_test_desktop", policy: policy)));
            Assert.Equal("native_screen_policy_denied", Assert.Single(denied.Native!.Diagnostics).Code); Assert.Null(denied.Native.Png);
            var masked = Value(await client.CaptureScreenAsync(new(target, output, "rendered", policy: new(output, screenshotMaskRegions: [new(0, 0, 50, 50)]))));
            using var partialMask = SKBitmap.Decode(masked.Rendered!.FilePath);
            Assert.Equal(SKColors.Black, partialMask.GetPixel(25, 25)); Assert.Equal(SKColors.Blue, partialMask.GetPixel(100, 60));
        });
    }

    [Fact]
    public async Task GenerationPolicyAndClosedSessionRejectEvidenceWithoutCreatingImages()
    {
        await WithWindow(async (runtime, window, target, client, output) =>
        {
            var stale = target with { };
            stale = new(target.SessionId, target.TopLevelId, topLevelGeneration: "old");
            Assert.Equal("screen_capture_stale", (await client.CaptureScreenAsync(new(stale, output))).Error!.Code);
            AutomationProperties.SetAutomationId(window, "private");
            var excluded = await runtime.CaptureScreenAsync(new(target, output, policy: new(output, excludedControlAutomationIds: ["private"])));
            Assert.Equal("screen_capture_excluded", excluded.Error!.Code);
            var other = await runtime.CaptureScreenAsync(new(target, output, policy: new(output, authorizedSessionIds: ["other"])));
            Assert.Equal("screen_capture_policy_denied", other.Error!.Code);
            var denied = await runtime.CaptureScreenAsync(new(target, output, policy: new(output, allowedActions: [])));
            Assert.False(denied.Success);
            var unsafePath = await client.CaptureScreenAsync(new(target, Path.GetTempPath(), "rendered", policy: new(output)));
            Assert.False(unsafePath.Success);
            Assert.False(Directory.Exists(output));
            runtime.SetNativeScreenCaptureScope("declared_test_desktop"); AvaScopeBridge.Deactivate();
            Assert.Null(runtime.NativeScreenCaptureScope);
            Assert.Throws<InvalidOperationException>(() => runtime.SetNativeScreenCaptureScope("declared_test_desktop"));
        });
    }

    [Fact]
    public async Task ExistingControlLeaseDoesNotPreventReadOnlyRenderAndOutputFailureIsPartial()
    {
        await WithWindow(async (runtime, window, target, client, output) =>
        {
            Assert.True((await client.SessionControlAsync(runtime.SessionId, new("acquire", "another-agent"))).Success);
            var observer = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
            Assert.Equal("captured", Value(await observer.CaptureScreenAsync(new(target, output, "rendered"))).Status);
            var file = Path.Combine(output, "not-a-directory"); File.WriteAllText(file, "do not overwrite");
            var failed = Value(await observer.CaptureScreenAsync(new(target, file, "rendered")));
            Assert.Equal("partial", failed.Status); Assert.Equal("screen_capture_save_failed", Assert.Single(failed.Rendered!.Diagnostics).Code);
            Assert.Equal("do not overwrite", File.ReadAllText(file)); Assert.Null(failed.Rendered.Png);
        });
    }

    [Fact]
    public void ContractRequiresPinnedTopLevelAbsolutePathAndBoundedExplicitModes()
    {
        var target = new RuntimeTargetContext(new("screen-contract"), "top", topLevelGeneration: "generation");
        Assert.Throws<ArgumentException>(() => new RuntimeScreenCaptureRequest(new(target.SessionId, target.TopLevelId), Path.GetTempPath()));
        Assert.Throws<ArgumentException>(() => new RuntimeScreenCaptureRequest(target, "relative"));
        Assert.Throws<ArgumentException>(() => new RuntimeScreenCaptureRequest(target, Path.GetTempPath(), "automatic"));
        Assert.Throws<ArgumentException>(() => new RuntimeScreenCaptureRequest(target, Path.GetTempPath(), desktopScope: "entire-unrelated-desktop"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeScreenCaptureRequest(target, Path.GetTempPath(), timeoutMs: 6000));
        Assert.False(BridgeIpcMethods.RequiresControl(new("capture", BridgeIpcMethods.CaptureScreen, screenCapture: new(target, Path.GetTempPath()))));
        var request = new RuntimeScreenCaptureRequest(target, Path.GetTempPath(), policy: new(Path.GetTempPath(), allowNativeScreenCapture: true));
        Assert.True(JsonSerializer.Deserialize<RuntimeScreenCaptureRequest>(JsonSerializer.Serialize(request))!.Policy!.AllowNativeScreenCapture);
    }

    private static RuntimeScreenCaptureResponse Value(CoreResult<RuntimeScreenCaptureResponse> result)
    { Assert.True(result.Success, JsonSerializer.Serialize(result.Error)); return result.Value!; }

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, Window, RuntimeTargetContext, LocalBridgeClient, string, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var output = Path.Combine(Path.GetTempPath(), "avascope-screen-" + Guid.NewGuid().ToString("N"));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var window = new Window { Width = 240, Height = 160, Background = Brushes.Blue, Content = new Border { Background = Brushes.Blue } };
                try
                {
                    window.Show(); using var registered = runtime.RegisterTopLevel(window); Dispatcher.UIThread.RunJobs();
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync()).Id;
                    var pinned = (await client.WindowAsync(new(new(runtime.SessionId, top)))).Value!.After!.Target;
                    await test(runtime, window, pinned, client, output);
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally
        {
            BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session);
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }
}
