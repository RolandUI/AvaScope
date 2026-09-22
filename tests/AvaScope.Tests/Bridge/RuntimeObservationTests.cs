using System.Diagnostics;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;
using SkiaSharp;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeObservationTests
{
    [Fact]
    public async Task CoordinatedSnapshotReportsFocusActionsChangesAndPartialCaptureFailure()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var output = TemporaryDirectory();
        try
        {
            await session.Dispatch(async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Observation"));
                var editor = new TextBox { Name = "Editor", Text = "Before" };
                var button = new Button { Name = "Apply", Content = "Apply" };
                var border = new RenderCallbackDecorator {
                    Child = new StackPanel { Children = { editor, button } } };
                var window = new Window { Width = 300, Height = 180, Content = border };
                try
                {
                    window.Show();
                    using var registration = runtime.RegisterTopLevel(window);
                    Dispatcher.UIThread.RunJobs();
                    editor.Focus();
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    var observer = new RuntimeObserver();
                    var stable = await observer.ObserveAsync(client, new RuntimeObservationRequest(runtime.SessionId,
                        [top.Id], maxDepth: 8, maxNodes: 128, maxInlineBytes: 131072));
                    Assert.True(stable.Success, stable.Error?.Message);
                    var observed = Assert.Single(stable.Value!.Windows);
                    Assert.False(stable.Value.ChangedDuringCollection);
                    Assert.Equal("no_sampled_change_detected", stable.Value.Consistency);
                    Assert.Equal(Assert.Single(observed.Nodes, node => node.Name == "Editor").NodeId, observed.FocusedNodeId);
                    Assert.Contains(InputActions.Invoke, Assert.Single(observed.Nodes, node => node.Name == "Apply").State.AvailableActions);
                    var tree = await runtime.GetVisualTreeAsync(top.Id, 8);
                    var inspection = await runtime.InspectNodeAsync(top.Id, TreeKinds.Visual, observed.FocusedNodeId!);
                    var separatePayload = JsonSerializer.SerializeToUtf8Bytes(tree.Value).Length
                        + JsonSerializer.SerializeToUtf8Bytes(inspection.Value).Length
                        + JsonSerializer.SerializeToUtf8Bytes(top).Length
                        + JsonSerializer.SerializeToUtf8Bytes(runtime.GetCapabilities()).Length;
                    Assert.True(JsonSerializer.SerializeToUtf8Bytes(stable.Value).Length < separatePayload);

                    border.OnRender = () => editor.Text = "Changed during collection";
                    var changed = await observer.ObserveAsync(client, new RuntimeObservationRequest(runtime.SessionId,
                        [top.Id], maxDepth: 8, maxNodes: 128, maxInlineBytes: 131072, includeScreenshot: true, outputDirectory: output));
                    Assert.True(changed.Success, changed.Error?.Message);
                    Assert.True(changed.Value!.ChangedDuringCollection);
                    var captured = Assert.Single(changed.Value.Windows);
                    Assert.Equal("available", captured.Parts["screenshot"]);
                    Assert.Null(captured.ScreenshotPng);
                    Assert.True(File.Exists(captured.Screenshot!.FilePath));
                    Assert.Equal("Before", Assert.Single(captured.Nodes, node => node.Name == "Editor").Text);
                    Assert.Equal("Changed during collection", editor.Text);

                    window.Hide();
                    var partial = await observer.ObserveAsync(client, new RuntimeObservationRequest(runtime.SessionId,
                        [top.Id, "topLevel:missing"], includeScreenshot: true, outputDirectory: output));
                    Assert.True(partial.Success, partial.Error?.Message);
                    Assert.Equal("unavailable", partial.Value!.Windows[0].Parts["screenshot"]);
                    Assert.NotEmpty(partial.Value.Windows[0].Nodes);
                    Assert.Contains(partial.Value.Windows[0].Diagnostics, error => error.Code == "observation_frame_unavailable");
                    Assert.Equal("unavailable", partial.Value.Windows[1].Parts["window"]);
                }
                finally
                {
                    window.Close();
                    AvaScopeBridge.Deactivate();
                }
            }, CancellationToken.None);
        }
        finally { Directory.Delete(output, recursive: true); }
    }

    [Fact]
    public async Task PolicyRedactsAllPartsAndMasksBeforeSavingBoundedArtifacts()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var root = TemporaryDirectory();
        const string secret = "observation-private-value";
        var longSecret = new string('q', 450) + "-secret-end";
        try
        {
            await session.Dispatch(async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Policy observation"));
                var panel = new StackPanel();
                var excluded = new TextBox { Text = "excluded-field-value" };
                AutomationProperties.SetAutomationId(excluded, "private-editor");
                panel.Children.Add(excluded);
                panel.Children.Add(new TextBlock { Text = longSecret });
                for (var index = 0; index < 80; index++) panel.Children.Add(new TextBlock { Text = $"{index}: {secret} public", Name = $"Row{index}" });
                var window = new Window { Width = 340, Height = 260, Title = secret, Content = panel };
                try
                {
                    window.Show();
                    using var registration = runtime.RegisterTopLevel(window);
                    excluded.Focus();
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    var request = new RuntimeObservationRequest(runtime.SessionId, [top.Id], maxDepth: 8, maxNodes: 200,
                        includeScreenshot: true, outputDirectory: Path.Combine(root, "run"), maxInlineBytes: 4096,
                        policy: new RuntimeEvidencePolicy(root, redactedText: [secret, longSecret], excludedControlAutomationIds: ["private-editor"]));
                    var result = await new RuntimeObserver().ObserveAsync(client, request);
                    Assert.True(result.Success, result.Error?.Message);
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(result.Value);
                    Assert.True(bytes.Length <= request.MaxInlineBytes);
                    var budget = Assert.IsType<ResponseBudgetInfo>(result.Value!.ResponseBudget);
                    Assert.True(budget.Truncated);
                    var artifact = File.ReadAllText(budget.ArtifactPath!);
                    Assert.DoesNotContain(secret, artifact, StringComparison.Ordinal);
                    Assert.DoesNotContain(new string('q', 384), artifact, StringComparison.Ordinal);
                    Assert.DoesNotContain("excluded-field-value", artifact, StringComparison.Ordinal);
                    Assert.DoesNotContain("screenshotPng", artifact, StringComparison.Ordinal);
                    var full = JsonSerializer.Deserialize<RuntimeObservationResponse>(artifact)!;
                    var observed = Assert.Single(full.Windows);
                    Assert.Null(observed.FocusedNodeId);
                    Assert.DoesNotContain(observed.Nodes, node => node.AutomationId == "private-editor");
                    Assert.Equal("full_sensitive_mask", observed.Parts["screenshotMasking"]);
                    using var image = SKBitmap.Decode(observed.Screenshot!.FilePath);
                    Assert.Equal(SKColors.Black, image.GetPixel(10, 10));
                    Assert.Equal(SKColors.Black, image.GetPixel(image.Width - 1, image.Height - 1));
                    foreach (var file in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
                        Assert.DoesNotContain(secret, File.ReadAllText(file), StringComparison.Ordinal);
                    var denied = await new RuntimeObserver().ObserveAsync(client,
                        new RuntimeObservationRequest(runtime.SessionId, [top.Id], outputDirectory: Path.Combine(root, "denied"),
                            policy: new RuntimeEvidencePolicy(root, authorizedSessionIds: ["different-session"])));
                    Assert.False(denied.Success);
                    Assert.False(Directory.Exists(Path.Combine(root, "denied")));
                }
                finally
                {
                    window.Close();
                    AvaScopeBridge.Deactivate();
                }
            }, CancellationToken.None);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ActualCliAndMcpProcessesExposeTheSameObservationContract()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var output = TemporaryDirectory();
        try
        {
            await session.Dispatch(async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Adapter observation"));
                var window = new Window { Width = 200, Height = 150, Content = new Button { Name = "Action", Content = "Act" } };
                try
                {
                    window.Show();
                    using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var request = new RuntimeObservationRequest(runtime.SessionId, [top.Id], maxDepth: 8);
                    var requestPath = Path.Combine(output, "request.json");
                    await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request));
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                    foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "observe", "--request", requestPath, "--manifest-dir", Path.GetDirectoryName(runtime.SessionManifestPath)! }) start.ArgumentList.Add(argument);
                    using var process = Process.Start(start)!;
                    var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
                    var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
                    await process.WaitForExitAsync(timeout.Token);
                    Assert.Equal(0, process.ExitCode);
                    Assert.True(string.IsNullOrWhiteSpace(await stderr));
                    var cli = JsonSerializer.Deserialize<ToolResult<RuntimeObservationResponse>>(await stdout)!;
                    Assert.True(cli.Success, cli.Error?.Message);
                    var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
                    if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
                    await using var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                    {
                        Name = "Observation validation", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                        InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3)
                    }), cancellationToken: timeout.Token);
                    var call = await client.CallToolAsync("observe", new Dictionary<string, object?>
                    {
                        ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = Path.GetDirectoryName(runtime.SessionManifestPath)
                    }, cancellationToken: timeout.Token);
                    var mcp = JsonSerializer.Deserialize<ToolResult<RuntimeObservationResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                    Assert.True(mcp.Success, mcp.Error?.Message);
                    Assert.Equal(cli.Value!.CapabilityRevision, mcp.Value!.CapabilityRevision);
                    Assert.Equal(cli.Value.Windows[0].Nodes.Select(node => node.NodeId), mcp.Value.Windows[0].Nodes.Select(node => node.NodeId));
                    Assert.Equal(cli.Value.Windows[0].Window, mcp.Value.Windows[0].Window);
                    Assert.NotEqual(cli.Value.ObservationId, mcp.Value.ObservationId);
                }
                finally
                {
                    window.Close();
                    AvaScopeBridge.Deactivate();
                }
            }, CancellationToken.None);
        }
        finally { Directory.Delete(output, recursive: true); }
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RenderCallbackDecorator : Decorator
    {
        public Action? OnRender { get; set; }
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.DrawRectangle(Brushes.White, null, new Avalonia.Rect(Bounds.Size));
            var action = OnRender;
            OnRender = null;
            action?.Invoke();
        }
    }
}
