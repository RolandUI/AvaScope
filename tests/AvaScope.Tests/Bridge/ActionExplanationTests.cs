using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class ActionExplanationTests
{
    [Fact]
    public async Task DisabledSaveSeparatesProvenBlockersDeclaredReasonsAndUnrelatedValidation()
    {
        await WithWindow(async (runtime, window, canvas, button, top, client) =>
        {
            var command = new TestCommand { Executable = false };
            button.Command = command;
            canvas.Children.Clear();
            var group = new Border { Child = button, IsEnabled = false, Width = 140, Height = 60 };
            canvas.Children.Add(group);
            var email = new TextBox { Name = "Email", Width = 100, Height = 30 };
            var unrelated = new TextBox { Name = "Unrelated", Width = 100, Height = 30 };
            Canvas.SetTop(email, 80); Canvas.SetTop(unrelated, 120);
            canvas.Children.Add(email); canvas.Children.Add(unrelated);
            DataValidationErrors.SetErrors(email, new[] { "Email is missing." });
            DataValidationErrors.SetErrors(unrelated, new[] { "Unrelated field error." });
            button.Context = new(CanExecute: false, BusinessReasons: ["The app declares that an email is required."], RelatedValidationTargets: [email]);
            Dispatcher.UIThread.RunJobs();
            var id = await Node(runtime, top, "Save");
            var focus = window.FocusManager!.GetFocusedElement();
            var result = await client.ExplainActionAsync(new(runtime.SessionId, top, id, "invoke"));
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal("blocked", result.Value!.Status);
            Assert.Contains(result.Value.Reasons, r => r.Code == "disabled_ancestor" && r.Certainty == "proven" && r.Target?.NodeId != id);
            Assert.Contains(result.Value.Reasons, r => r.Code == "command_denied" && !r.Message.Contains("email"));
            Assert.Contains(result.Value.Reasons, r => r.Code == "app_business_reason" && r.Certainty == "app_declared");
            Assert.Contains(result.Value.Reasons, r => r.Code == "validation_error" && r.Message.StartsWith("app_declared_relation:") && !r.BlocksAction);
            Assert.Contains(result.Value.Reasons, r => r.Code == "validation_error" && r.Message.StartsWith("same_window_unproven:") && !r.BlocksAction);
            Assert.Same(focus, window.FocusManager.GetFocusedElement());
            Assert.Equal(0, command.Executions);
            AutomationProperties.SetAutomationId(email, "excluded-email");
            var hiddenField = await client.ExplainActionAsync(new(runtime.SessionId, top, id, "invoke",
                policy: new RuntimeEvidencePolicy(Path.GetTempPath(), excludedControlAutomationIds: ["excluded-email"])));
            Assert.DoesNotContain("Email is missing", JsonSerializer.Serialize(hiddenField.Value));
            Assert.DoesNotContain("excluded-email", JsonSerializer.Serialize(hiddenField.Value));
            var excludedTarget = await client.ExplainActionAsync(new(runtime.SessionId, top, id,
                policy: new RuntimeEvidencePolicy(Path.GetTempPath(), excludedControlAutomationIds: ["save"])));
            Assert.False(excludedTarget.Success);
            Assert.Equal("action_target_excluded", excludedTarget.Error!.Code);
            Assert.False((await runtime.ValidateInputAsync(top, "invoke", targetNodeId: id)).Success);
            Assert.False((await runtime.InputAsync(top, "invoke", targetNodeId: id)).Success);
            group.IsEnabled = true;
            command.Executable = true;
            command.Changed();
            button.Context = new(RelatedValidationTargets: [email]);
            Dispatcher.UIThread.RunJobs();
            var enabled = await client.ExplainActionAsync(new(runtime.SessionId, top, id, "invoke"));
            Assert.Equal("no_observed_blocker", enabled.Value!.Status);
            Assert.All(enabled.Value.Reasons.Where(r => r.Code == "validation_error"), r => Assert.False(r.BlocksAction));
            Assert.Equal(0, command.Executions);
        });
    }

    [Fact]
    public async Task ModalOwnershipAndUnavailableNativeEvidenceAreExplicitWithoutClosingOrFocusing()
    {
        await WithWindow(async (runtime, window, _, button, top, client) =>
        {
            var clicked = 0;
            button.Click += (_, _) => clicked++;
            var id = await Node(runtime, top, "Save");
            var dialog = new Window { Width = 180, Height = 100, Title = "Confirm" };
            try
            {
                var dialogTask = dialog.ShowDialog(window);
                using var registration = runtime.RegisterTopLevel(dialog);
                Dispatcher.UIThread.RunJobs();
                var result = await client.ExplainActionAsync(new(runtime.SessionId, top, id, "invoke"));
                Assert.Contains(result.Value!.Reasons, r => r.Code == "modal_owner_blocked" && r.Certainty == "proven" && r.Target!.TopLevelId != top);
                Assert.True(dialog.IsVisible);
                Assert.False((await runtime.InputAsync(top, "invoke", targetNodeId: id)).Success);
                Assert.False((await runtime.InputAsync(top, "click", targetNodeId: id, execution: new())).Success);
                Assert.Equal(0, clicked);
            }
            finally { dialog.Close(); }
            Dispatcher.UIThread.RunJobs();
            var native = await client.ExplainActionAsync(new(runtime.SessionId, top, id, strategy: "native"));
            Assert.Equal("unavailable", native.Value!.NativeConfirmation);
            Assert.Contains(native.Value.Reasons, r => r.Code == "native_route_unavailable");
            var semantic = await client.ExplainActionAsync(new(runtime.SessionId, top, id, "invoke"));
            Assert.DoesNotContain(semantic.Value!.Reasons, r => r.Code == "modal_owner_blocked");
            Assert.Contains(semantic.Value.Reasons, r => r.Code == "command_reason_unavailable" && r.Certainty == "unknown");
            Assert.Equal(0, clicked);
        });
    }

    [Fact]
    public async Task DeclaredPointAvoidsAHoleAndRejectsClipsAndOverlaysBeforeDispatch()
    {
        await WithWindow(async (runtime, _, canvas, button, top, _) =>
        {
            button.Clip = new CombinedGeometry(GeometryCombineMode.Exclude,
                new RectangleGeometry(new Rect(0, 0, 140, 60)), new RectangleGeometry(new Rect(40, 15, 60, 30)));
            var pressed = 0;
            button.AddHandler(InputElement.PointerPressedEvent, (_, _) => pressed++, handledEventsToo: true);
            Dispatcher.UIThread.RunJobs();
            var id = await Node(runtime, top, "Save");
            var center = await runtime.ExplainActionAsync(new(runtime.SessionId, top, id, strategy: "synthetic"));
            Assert.Equal("bounds_center", center.Value!.ActivationPoint.Source);
            Assert.Equal("clipped", center.Value.ActivationPoint.Status);
            Assert.Equal(0, pressed);
            button.Context = new(new Point(10, 10));
            var declared = await runtime.ExplainActionAsync(new(runtime.SessionId, top, id, strategy: "synthetic"));
            Assert.Equal("valid", declared.Value!.ActivationPoint.Status);
            Assert.Equal("app_declared_local_dip", declared.Value.ActivationPoint.Source);
            Assert.Equal("top_level_dip", declared.Value.ActivationPoint.CoordinateSpace);
            var sent = await runtime.InputAsync(top, "click", targetNodeId: id,
                execution: new() { ExpectedGeometryRevision = declared.Value.ActivationPoint.GeometryRevision });
            Assert.True(sent.Success, sent.Error?.Message);
            Assert.Equal(1, pressed);
            Assert.Equal(declared.Value.ActivationPoint.GeometryRevision, sent.Value!.ActivationPoint!.GeometryRevision);
            var legacy = await runtime.InputAsync(top, "click", targetNodeId: id);
            Assert.True(legacy.Success, legacy.Error?.Message);
            Assert.Equal("app_declared_local_dip", legacy.Value!.ActivationPoint!.Source);

            var overlay = new Border { Name = "Overlay", Width = 140, Height = 60, Background = Brushes.Red };
            canvas.Children.Add(overlay);
            Dispatcher.UIThread.RunJobs();
            Assert.True((await runtime.ReadinessAsync(top, options: new(waitForFrame: true))).Success);
            var covered = await runtime.ExplainActionAsync(new(runtime.SessionId, top, id, strategy: "synthetic"));
            Assert.Equal("obstructed", covered.Value!.ActivationPoint.Status);
            Assert.Contains(covered.Value.Reasons, r => r.Code == "hit_test_obstruction" && r.Target?.NodeId == covered.Value.ActivationPoint.HitTarget!.NodeId);
            var rejected = await runtime.InputAsync(top, "click", targetNodeId: id, execution: new());
            Assert.False(rejected.Success);
            Assert.Equal("0", rejected.Error!.Details!["dispatchedEvents"]);
            Assert.Equal(1, pressed);
            overlay.IsHitTestVisible = false;
            Dispatcher.UIThread.RunJobs();
            Assert.True((await runtime.ReadinessAsync(top, options: new(waitForFrame: true))).Success);
            Assert.Equal("valid", (await runtime.ExplainActionAsync(new(runtime.SessionId, top, id, strategy: "synthetic"))).Value!.ActivationPoint.Status);
            button.Context = new(new Point(double.NaN, 10));
            Assert.Equal("invalid", (await runtime.ExplainActionAsync(new(runtime.SessionId, top, id))).Value!.ActivationPoint.Status);
            Assert.False((await runtime.InputAsync(top, "click", targetNodeId: id, execution: new())).Success);

            button.Context = new(new Point(10, 10));
            var other = new TextBox { Width = 80, Height = 30 };
            Canvas.SetTop(other, 120);
            canvas.Children.Add(other);
            Dispatcher.UIThread.RunJobs();
            other.Focus();
            button.GotFocus += (_, _) => overlay.IsHitTestVisible = true;
            var clicked = 0;
            button.Click += (_, _) => clicked++;
            var changedOnFocus = await runtime.InputAsync(top, "click", targetNodeId: id);
            Assert.False(changedOnFocus.Success);
            Assert.Equal(0, clicked);
        });
    }

    [Fact]
    public async Task GeometryRevisionsIncludeMovementScalingAndCurrentLayoutAndNeverReplayAPartialClick()
    {
        await WithWindow(async (runtime, window, _, button, top, _) =>
        {
            button.Context = new(new Point(10, 10));
            var id = await Node(runtime, top, "Save");
            var first = (await runtime.ExplainActionAsync(new(runtime.SessionId, top, id, strategy: "synthetic"))).Value!;
            window.Position = new PixelPoint(80, 90);
            window.SetRenderScaling(2);
            Canvas.SetLeft(button, 30);
            Dispatcher.UIThread.RunJobs();
            var second = (await runtime.ExplainActionAsync(new(runtime.SessionId, top, id, strategy: "synthetic"))).Value!;
            Assert.Equal(2, second.ActivationPoint.RenderScaling);
            Assert.NotEqual(first.ActivationPoint.GeometryRevision, second.ActivationPoint.GeometryRevision);
            Assert.Equal(40, second.ActivationPoint.X);
            var stale = await runtime.InputAsync(top, "click", targetNodeId: id,
                execution: new() { ExpectedGeometryRevision = first.ActivationPoint.GeometryRevision });
            Assert.False(stale.Success);
            Assert.Equal("0", stale.Error!.Details!["dispatchedEvents"]);
            var clicks = 0;
            button.Click += (_, _) => { clicks++; Canvas.SetLeft(button, 60); };
            var partial = await runtime.InputAsync(top, "click", targetNodeId: id,
                execution: new() { ClickCount = 2, IntervalMs = 20, ExpectedGeometryRevision = second.ActivationPoint.GeometryRevision });
            Assert.False(partial.Success);
            Assert.Equal(1, clicks);
            Assert.Equal("2", partial.Error!.Details!["dispatchedEvents"]);
            Assert.Contains("before pointer dispatch", partial.Error.Message);
        });
    }

    [Fact]
    public async Task CliAndMcpReturnTheSameBoundedRedactedEvidenceAndRejectStaleIdentity()
    {
        await WithWindow(async (runtime, _, _, button, top, client) =>
        {
            var secret = "private-business-canary" + new string('x', 600);
            button.Context = new(CanExecute: false, BusinessReasons: Enumerable.Repeat(secret, 20).ToArray());
            var id = await Node(runtime, top, "Save");
            var root = Path.Combine(Path.GetTempPath(), "avascope-explain-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var request = new RuntimeActionExplanationRequest(runtime.SessionId, top, id, "invoke",
                    policy: new RuntimeEvidencePolicy(root, redactedText: [secret]), maxReasons: 5);
                var direct = await client.ExplainActionAsync(request);
                Assert.True(direct.Success, direct.Error?.Message);
                Assert.True(direct.Value!.Truncated);
                Assert.Equal(5, direct.Value.Reasons.Count);
                Assert.DoesNotContain("private-business-canary", JsonSerializer.Serialize(direct.Value));
                var file = Path.Combine(root, "request.json");
                await File.WriteAllTextAsync(file, JsonSerializer.Serialize(request));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "explain-action", "--request", file, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(arg);
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                try { await process.WaitForExitAsync(timeout.Token); }
                finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                Assert.Equal(0, process.ExitCode);
                var cli = JsonSerializer.Deserialize<ToolResult<RuntimeActionExplanation>>(await output)!;
                Assert.True(cli.Success, cli.Error?.Message + await errors);
                Assert.Equal(direct.Value.Reasons.Select(r => (r.Code, r.Message)), cli.Value!.Reasons.Select(r => (r.Code, r.Message)));
                var environment = TestEnvironment.McpEnvironment();
                if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "Action explanation parity", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                    InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3)
                }), cancellationToken: timeout.Token);
                var called = await mcp.CallToolAsync("explain_action", new Dictionary<string, object?>
                {
                    ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = client.ManifestDirectory
                }, cancellationToken: timeout.Token);
                var actual = JsonSerializer.Deserialize<ToolResult<RuntimeActionExplanation>>(JsonSerializer.Serialize(called.StructuredContent))!;
                Assert.True(actual.Success, actual.Error?.Message);
                Assert.Equal(cli.Value.Reasons.Select(r => (r.Code, r.Message)), actual.Value!.Reasons.Select(r => (r.Code, r.Message)));
                Assert.DoesNotContain("private-business-canary", JsonSerializer.Serialize(called.StructuredContent));
                var target = new RuntimeTargetContext(runtime.SessionId, top, TreeKinds.Visual, id, nodeGeneration: "stale");
                Assert.False((await client.ExplainActionAsync(new(runtime.SessionId, top, id, target: target))).Success);
                button.ThrowEvidence = true;
                var failed = await runtime.ExplainActionAsync(new(runtime.SessionId, top, id));
                Assert.Contains(failed.Value!.Reasons, r => r.Code == "app_context_unavailable" && r.Certainty == "unknown");
                Assert.Equal("unavailable", failed.Value.ActivationPoint.Status);
            }
            finally { Directory.Delete(root, recursive: true); }
        });
    }

    private static async Task<string> Node(AvaScopeBridgeRuntime runtime, string top, string name) =>
        Assert.Single((await runtime.FindNodesAsync(top, TreeKinds.Visual, name: name)).Value!.Matches).Node.NodeId;

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, Window, Canvas, EvidenceButton, string, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate();
                var button = new EvidenceButton { Name = "Save", Content = "Save", Width = 140, Height = 60, Background = Brushes.White };
                AutomationProperties.SetAutomationId(button, "save");
                var canvas = new Canvas { Background = Brushes.White, Children = { button } };
                var window = new Window { Width = 320, Height = 240, Content = canvas };
                try
                {
                    window.Show();
                    using var registration = runtime.RegisterTopLevel(window);
                    Dispatcher.UIThread.RunJobs();
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    Assert.True((await runtime.ReadinessAsync(top.Id, options: new(waitForFrame: true))).Success);
                    await test(runtime, window, canvas, button, top.Id, new(Path.GetDirectoryName(runtime.SessionManifestPath)!));
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    private sealed class EvidenceButton : Button, IAvaScopeActionContextProvider
    {
        protected override Type StyleKeyOverride => typeof(Button);
        public AvaScopeActionContext Context { get; set; } = new();
        public bool ThrowEvidence { get; set; }
        public AvaScopeActionContext GetActionContext(string action) => ThrowEvidence ? throw new InvalidOperationException("private-error-canary") : Context;
    }

    private sealed class TestCommand : ICommand
    {
        public bool Executable { get; set; }
        public int Executions { get; private set; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => Executable;
        public void Execute(object? parameter) => Executions++;
        public void Changed() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
