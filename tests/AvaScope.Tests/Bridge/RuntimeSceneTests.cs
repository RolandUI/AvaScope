using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeSceneTests
{
    [Fact]
    public async Task DeclaredConnectionsExposeTransformsAndSelectThroughTheExistingAuthorizedAction()
    {
        await WithScene(async (runtime, graph, target, client) =>
        {
            graph.Camera = new Matrix(2, 0, 0, 2, 10, 20);
            var before = Snapshot(await client.SceneAsync(new(target, objectType: "connection", relatedTo: "a")));
            var connection = Assert.Single(before.Objects);
            Assert.Equal("a-b", connection.Object.Id); Assert.Equal("connects_from", connection.Object.Relationships![0].Kind);
            Assert.False(connection.Object.Selected); Assert.NotNull(connection.Target);
            Assert.Equal(new NodeBounds(50, 40, 160, 4), connection.CanvasBounds);
            Assert.Equal(connection.CanvasBounds, connection.TopLevelBounds);
            Assert.Contains("not_native_hit_test", before.Provenance);
            var selected = await client.SceneAsync(Invoke(target, connection.Target!));
            Assert.True(OperationResultMapper.IsSuccessful(selected), JsonSerializer.Serialize(selected));
            Assert.Equal("executed", selected.Value!.Status); Assert.True(selected.Value.Action!.Executed);
            Assert.Equal("a-b", graph.Selected); Assert.Equal(1, graph.Calls);
            Assert.True(Assert.Single(selected.Value.Snapshot!.Objects, item => item.Object.Id == "a-b").Object.Selected);
            Assert.NotEqual(before.Revision, selected.Value.Snapshot.Revision);
            var direct = await client.InvokeCustomActionAsync(runtime.SessionId, new("unguarded", target, "select"));
            Assert.False(direct.Value!.Executed); Assert.Contains(direct.Value.Diagnostics, item => item.Code == "scene_object_required");
            Assert.Equal(1, graph.Calls);
            Assert.True(Assert.Single((await runtime.ListCustomActionsAsync(target)).Value!.Actions).RequiresSceneObject);
        });
    }

    [Fact]
    public async Task CameraLayoutRemovalReuseRedrawAndAvailabilityCallbacksInvalidateOldTargets()
    {
        await WithScene(async (_, graph, target, client) =>
        {
            async Task<RuntimeSceneObjectTarget> Connection() => Assert.Single(Snapshot(await client.SceneAsync(new(target, objectId: "a-b"))).Objects).Target!;
            var old = await Connection(); graph.Camera = Matrix.CreateTranslation(5, 10);
            Rejected(await client.SceneAsync(Invoke(target, old)), "scene_stale");
            old = await Connection(); graph.Revision++;
            Rejected(await client.SceneAsync(Invoke(target, old)), "scene_stale");
            old = await Connection(); graph.DataContext = new object();
            Rejected(await client.SceneAsync(Invoke(target, old)), "scene_stale");
            old = await Connection(); graph.ConnectionGeneration = "replaced";
            Rejected(await client.SceneAsync(Invoke(target, old)), "scene_stale");
            old = await Connection(); graph.IncludeConnection = false;
            Rejected(await client.SceneAsync(Invoke(target, old)), "scene_stale"); graph.IncludeConnection = true;
            old = await Connection(); graph.Margin = new Thickness(12); Dispatcher.UIThread.RunJobs();
            Rejected(await client.SceneAsync(Invoke(target, old)), "scene_stale");
            old = await Connection(); graph.OnAvailability = () => graph.Camera = Matrix.CreateTranslation(40, 5);
            Rejected(await client.SceneAsync(Invoke(target, old)), "scene_stale"); graph.OnAvailability = null;
            Assert.Equal(0, graph.Calls);
            graph.ChangeEveryCapture = true;
            Rejected(await client.SceneAsync(new(target)), "scene_changed_during_capture");
        });
    }

    [Fact]
    public async Task PolicyRedactsBeforeTruncationAndExcludesObjectsAncestorsAndPrivateRelations()
    {
        await WithScene(async (runtime, graph, target, client) =>
        {
            var raw = Snapshot(await client.SceneAsync(new(target, objectId: "a-b")));
            graph.Label = new string('x', 508) + "secret";
            graph.PrivateEndpoint = true;
            var policy = Policy(runtime, redacted: ["secret"], excluded: ["private-node"]);
            var scene = Snapshot(await client.SceneAsync(new(target, policy: policy)));
            Assert.Equal("partial", scene.Coverage); Assert.Contains("policy_exclusions", scene.Unavailable);
            Assert.DoesNotContain(scene.Objects, item => item.Object.Id == "b");
            var connection = Assert.Single(scene.Objects, item => item.Object.Id == "a-b");
            Assert.Single(connection.Object.Relationships!); Assert.Contains("relationships_incomplete", connection.Unavailable);
            Assert.DoesNotContain("secret", JsonSerializer.Serialize(scene)); Assert.DoesNotContain("secr", connection.Object.Label!);
            var noAction = new RuntimeEvidencePolicy(policy.OwnedEvidenceRoot, authorizedSessionIds: [runtime.SessionId.Value]);
            Assert.False((await client.SceneAsync(Invoke(target, raw.Objects[0].Target!, noAction))).Success);
            AutomationProperties.SetAutomationId(graph.Parent!, "private-node");
            Rejected(await client.SceneAsync(new(target, policy: policy)), "scene_excluded");
            AutomationProperties.SetAutomationId(graph.Parent!, null);
            graph.Label = "Connection"; graph.PrivateEndpoint = false;
            var identityPolicy = Policy(runtime, redacted: ["a-b"]);
            var hidden = Snapshot(await client.SceneAsync(new(target, objectId: "a-b", policy: identityPolicy)));
            Assert.Empty(hidden.Objects); Assert.DoesNotContain("a-b", JsonSerializer.Serialize(hidden));
        });
    }

    [Fact]
    public async Task PartialScenesInvalidAdaptersUnsupportedControlsDisposalAndRestartAreExplicit()
    {
        await WithScene(async (runtime, graph, target, client) =>
        {
            graph.ExtraObjects = 300;
            var partial = Snapshot(await client.SceneAsync(new(target, maxObjects: 2)));
            Assert.Equal(256, partial.ExaminedObjects); Assert.Equal(2, partial.Objects.Count); Assert.Equal("partial", partial.Coverage);
            Assert.Contains("scene_coverage_incomplete", partial.Unavailable); Assert.Contains("response_object_limit", partial.Unavailable);
            Assert.Empty(Snapshot(await client.SceneAsync(new(target, objectId: "item-299"))).Objects);
            graph.ExtraObjects = 0; graph.InvalidDuplicate = true;
            Rejected(await client.SceneAsync(new(target)), "scene_duplicate_identity"); graph.InvalidDuplicate = false;
            graph.ThrowCapture = true;
            var failure = Rejected(await client.SceneAsync(new(target)), "scene_adapter_failed");
            Assert.DoesNotContain("adapter-secret", JsonSerializer.Serialize(failure)); graph.ThrowCapture = false;
            var old = Assert.Single(Snapshot(await client.SceneAsync(new(target, objectId: "a-b"))).Objects).Target!;
            graph.Registration!.Dispose();
            Rejected(await client.SceneAsync(new(target)), "scene_unsupported");
            using var replacement = runtime.RegisterScene(graph, graph.Capture);
            Rejected(await client.SceneAsync(Invoke(target, old)), "scene_stale");
            var root = (Panel)graph.Parent!; var other = new Canvas { Name = "Other", Width = 100, Height = 100 }; root.Children.Add(other);
            var otherTarget = Assert.Single((await runtime.FindNodesAsync(target.TopLevelId, TreeKinds.Visual, name: "Other")).Value!.Matches).Target!;
            Rejected(await client.SceneAsync(new(otherTarget)), "scene_unsupported");
            root.Children.Remove(graph);
            Rejected(await client.SceneAsync(new(target)), "scene_canvas_stale");
            AvaScopeBridge.Deactivate();
            var next = AvaScopeBridge.Activate();
            Assert.Equal("scene_session_mismatch", (await next.SceneAsync(new(target))).Error!.Code);
        });
    }

    [Fact]
    public async Task CliMcpAndControlLeaseUseTheSameSceneTargetsAndPrivacyContract()
    {
        await WithScene(async (runtime, graph, target, client) =>
        {
            var policy = Policy(runtime, redacted: ["private-label"]); graph.Label = "private-label";
            var request = new RuntimeSceneRequest(target, objectId: "a-b", policy: policy);
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "scene", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(arg);
                using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                var cli = JsonSerializer.Deserialize<ToolResult<RuntimeSceneResponse>>(await stdout)!;
                Assert.True(cli.Success, await stdout + await stderr); Assert.Equal(0, process.ExitCode); Assert.DoesNotContain("private-label", await stdout);
                var expected = Assert.Single(cli.Value!.Snapshot!.Objects).Target!;
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
                { Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "scene-test" }), cancellationToken: timeout.Token);
                var invoke = Invoke(target, expected, policy);
                var call = await mcp.CallToolAsync("scene", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(invoke), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var selected = JsonSerializer.Deserialize<ToolResult<RuntimeSceneResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(selected.Success, JsonSerializer.Serialize(selected)); Assert.Equal("a-b", graph.Selected);
                Assert.DoesNotContain("private-label", JsonSerializer.Serialize(selected));
                call = await mcp.CallToolAsync("scene", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(invoke), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var stale = JsonSerializer.Deserialize<ToolResult<RuntimeSceneResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.False(stale.Success); Assert.Equal("scene_stale", stale.Error!.Code); Assert.Equal(1, graph.Calls);
                Assert.True((await client.SessionControlAsync(runtime.SessionId, new("acquire", "scene-owner"))).Success);
                var outsider = new LocalBridgeClient(client.ManifestDirectory);
                Assert.NotNull(Snapshot(await outsider.SceneAsync(new(target))));
                Assert.Equal("session_control_conflict", (await outsider.SceneAsync(invoke)).Error!.Code);
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public void RequestsRequirePinnedCanvasExplicitObjectIdentityAndBoundedOptions()
    {
        var target = new RuntimeTargetContext(new("session"), "top", "visual", "canvas", topLevelGeneration: "t", nodeGeneration: "n");
        var expected = new RuntimeSceneObjectTarget("scene", new string('a', 64), "object", "generation");
        Assert.Throws<ArgumentException>(() => new RuntimeSceneRequest(new(new("s"), "top", "visual", "node")));
        Assert.Throws<ArgumentException>(() => new RuntimeSceneRequest(target, "invoke", actionName: "select", requestId: "id"));
        Assert.Throws<ArgumentException>(() => new RuntimeSceneRequest(target, expectedObject: expected));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeSceneRequest(target, maxObjects: 129));
        Assert.Throws<ArgumentException>(() => new RuntimeSceneObjectTarget("scene", "old", "object", "generation"));
        Assert.False(BridgeIpcMethods.RequiresControl(new("read", BridgeIpcMethods.Scene, scene: new(target))));
        Assert.True(BridgeIpcMethods.RequiresControl(new("invoke", BridgeIpcMethods.Scene, scene: Invoke(target, expected))));
    }

    private static RuntimeSceneRequest Invoke(RuntimeTargetContext canvas, RuntimeSceneObjectTarget expected, RuntimeEvidencePolicy? policy = null)
        => new(canvas, "invoke", expectedObject: expected, actionName: "select", requestId: Guid.NewGuid().ToString("N"), policy: policy);

    private static RuntimeEvidencePolicy Policy(AvaScopeBridgeRuntime runtime, IReadOnlyList<string>? redacted = null, IReadOnlyList<string>? excluded = null)
        => new(Path.Combine(Path.GetTempPath(), "avascope-scene-tests"), redactedText: redacted, excludedControlAutomationIds: excluded,
            authorizedSessionIds: [runtime.SessionId.Value], allowedActions: [SemanticWorkflowActions.Inspect, SemanticWorkflowActions.CustomAction], allowedCustomActions: ["select"]);

    private static RuntimeSceneSnapshot Snapshot(CoreResult<RuntimeSceneResponse> result)
    {
        Assert.True(OperationResultMapper.IsSuccessful(result), JsonSerializer.Serialize(result));
        Assert.NotNull(result.Value!.Snapshot); return result.Value.Snapshot;
    }

    private static ToolResult<RuntimeSceneResponse> Rejected(CoreResult<RuntimeSceneResponse> result, string code)
    {
        var tool = OperationResultMapper.ToToolResult(result); Assert.False(tool.Success); Assert.Equal(code, tool.Error!.Code);
        Assert.False(tool.Value?.Action?.Executed ?? false); return tool;
    }

    private static async Task WithScene(Func<AvaScopeBridgeRuntime, Graph, RuntimeTargetContext, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate(new(enableCustomActions: true, allowedCustomActions: ["select"]));
                var graph = new Graph { Name = "Graph", Width = 400, Height = 250 };
                var root = new Canvas { Children = { graph } }; var window = new Window { Width = 500, Height = 400, Content = root };
                try
                {
                    window.Show(); using var registration = runtime.RegisterTopLevel(window); Dispatcher.UIThread.RunJobs();
                    graph.Registration = runtime.RegisterScene(graph, graph.Capture);
                    using var scene = graph.Registration;
                    using var action = runtime.RegisterCustomAction(graph, new("select", context =>
                    {
                        graph.Selected = context.SceneObject!.Id; graph.Calls++; graph.Revision++;
                        return CustomActionOutcome.Succeeded("Selection updated.");
                    }, availability: _ => { graph.OnAvailability?.Invoke(); return CustomActionAvailability.Available; }, requiresSceneObject: true));
                    var top = Assert.Single(await runtime.ListTopLevelsAsync()).Id;
                    var target = Assert.Single((await runtime.FindNodesAsync(top, TreeKinds.Visual, name: "Graph")).Value!.Matches).Target!;
                    await test(runtime, graph, target, new(Path.GetDirectoryName(runtime.SessionManifestPath)!));
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    private sealed class Graph : Control
    {
        public int Revision { get; set; }
        public Matrix Camera { get; set; } = Matrix.Identity;
        public string? Selected { get; set; }
        public string Label { get; set; } = "A to B";
        public string ConnectionGeneration { get; set; } = "ab-1";
        public bool IncludeConnection { get; set; } = true;
        public bool ChangeEveryCapture { get; set; }
        public bool PrivateEndpoint { get; set; }
        public bool InvalidDuplicate { get; set; }
        public bool ThrowCapture { get; set; }
        public int ExtraObjects { get; set; }
        public int Calls { get; set; }
        public Action? OnAvailability { get; set; }
        public IDisposable? Registration { get; set; }

        public AvaScopeSceneSnapshot Capture()
        {
            if (ThrowCapture) throw new InvalidOperationException("adapter-secret");
            if (ChangeEveryCapture) Revision++;
            var objects = new List<RuntimeSceneObject>
            {
                new("a", "a-1", "node", "A", new(0, 0, 20, 20), Selected == "a"),
                new("b", "b-1", "node", "B", new(100, 0, 20, 20), Selected == "b", PrivacyAutomationId: PrivateEndpoint ? "private-node" : null)
            };
            if (IncludeConnection) objects.Add(new("a-b", ConnectionGeneration, "connection", Label, new(20, 10, 80, 2), Selected == "a-b",
                [new("connects_from", "a", "a-1"), new("connects_to", "b", "b-1")], ["select"]));
            if (InvalidDuplicate) objects.Add(objects[0]);
            objects.AddRange(Enumerable.Range(0, ExtraObjects).Select(index => new RuntimeSceneObject("item-" + index, "generation", "point")));
            return new(Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), objects, Camera);
        }
    }
}
