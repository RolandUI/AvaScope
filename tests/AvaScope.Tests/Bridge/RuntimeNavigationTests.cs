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
public sealed class RuntimeNavigationTests
{
    [Fact]
    public async Task BranchesAndRepeatedBackForwardRoutesRemainObservedPathsWithoutReplay()
    {
        await WithNavigation(async (runtime, view, target, client, _) =>
        {
            var run = Value(await client.NavigationAsync(new(runtime.SessionId, "start", identityTarget: target)));
            var first = run.CurrentVisitId!;
            foreach (var surface in new[] { "settings", "home", "settings", "home" })
            {
                view.Surface = surface; view.Text.Text = surface;
                run = Value(await client.NavigationAsync(Record(runtime, run, target, surface)));
            }
            var loopEnd = run.CurrentVisitId!;
            Assert.Contains(run.Loops, loop => loop.Length == 2 && loop.Repetitions == 2 && loop.Confidence == "host_declared_and_sampled_revisit");
            var route = Value(await client.NavigationAsync(new(runtime.SessionId, runId: run.RunId, fromVisitId: first, toVisitId: loopEnd)));
            Assert.Equal("reported_success", route.Route!.Status); Assert.True(route.Route.Complete);
            Assert.Equal(5, route.Route.VisitIds.Count); Assert.Equal(4, route.Route.Steps.Count);
            Assert.All(route.Route.Steps, step => Assert.Contains("causality_not_verified", step.Provenance));
            Assert.Contains("not_a_future_execution_plan", route.Route.Provenance);
            view.Surface = "details"; view.Text.Text = "details";
            run = Value(await client.NavigationAsync(new(runtime.SessionId, "record", run.RunId, identityTarget: target,
                previousVisitId: run.CurrentVisitId, transition: new("open details", "failed"))));
            route = Value(await client.NavigationAsync(new(runtime.SessionId, runId: run.RunId, fromVisitId: loopEnd, toVisitId: run.CurrentVisitId)));
            Assert.Equal("includes_failed_or_unknown", route.Route!.Status);
            Assert.Equal("navigation_tip_changed", (await client.NavigationAsync(new(runtime.SessionId, "record", run.RunId, previousVisitId: first))).Error!.Code);
            Assert.Equal(0, view.Clicks);
        });
    }

    [Fact]
    public async Task DifferentDocumentsRevisionsContentAndModalWindowsDoNotMergeStates()
    {
        await WithNavigation(async (runtime, view, target, client, window) =>
        {
            var run = Value(await client.NavigationAsync(new(runtime.SessionId, "start", maxNodes: 64, maxDepth: 8, identityTarget: target, includeEvidence: true)));
            Assert.Contains(run.Visits[0].Observation!.Windows.SelectMany(item => item.Nodes), node => node.Text == "home");
            var keys = new HashSet<string> { run.Visits[0].StateKey };
            view.Context = "document-b";
            run = Value(await client.NavigationAsync(Record(runtime, run, target, "other document"))); Assert.True(keys.Add(run.Visits[0].StateKey));
            view.Revision = "2";
            run = Value(await client.NavigationAsync(Record(runtime, run, target, "document revised"))); Assert.True(keys.Add(run.Visits[0].StateKey));
            view.Text.Text = "changed visible document content";
            run = Value(await client.NavigationAsync(Record(runtime, run, target, "content changed"))); Assert.True(keys.Add(run.Visits[0].StateKey));
            view.DataContext = new object();
            run = Value(await client.NavigationAsync(Record(runtime, run, target, "context recycled"))); Assert.True(keys.Add(run.Visits[0].StateKey));
            var dialog = new Window { Title = "Confirm document", Content = new TextBlock { Text = "Are you sure?" }, Width = 220, Height = 100 };
            using var registration = runtime.RegisterTopLevel(dialog);
            var task = dialog.ShowDialog(window);
            try
            {
                Dispatcher.UIThread.RunJobs();
                run = Value(await client.NavigationAsync(Record(runtime, run, target, "open modal")));
                Assert.True(keys.Add(run.Visits[0].StateKey));
                var evidence = Value(await client.NavigationAsync(new(runtime.SessionId, runId: run.RunId, visitId: run.CurrentVisitId, includeEvidence: true)));
                Assert.Equal(2, Assert.Single(evidence.Visits).Observation!.Windows.Count);
            }
            finally { dialog.Close(); await task; }
            Assert.Equal(6, run.RetainedVisits);
        });
    }

    [Fact]
    public async Task SimilarSamplesWithoutHostIdentityStaySeparateAndOnlySuggestUncertainRevisits()
    {
        await WithNavigation(async (runtime, view, _, client, _) =>
        {
            var run = Value(await client.NavigationAsync(new(runtime.SessionId, "start", maxNodes: 64, maxDepth: 8, includeEvidence: true)));
            Assert.Contains(run.Visits[0].Observation!.Windows.SelectMany(item => item.Nodes), node => node.Text == "home");
            var first = run.Visits[0];
            run = Value(await client.NavigationAsync(Record(runtime, run, null, "back to an apparently similar surface")));
            var second = run.Visits[0];
            Assert.NotEqual(first.StateKey, second.StateKey); Assert.NotEqual(first.VisitId, second.VisitId);
            Assert.Equal(first.ObservationRevision, second.ObservationRevision);
            Assert.Contains("uncertain", Assert.Single(run.Loops).Confidence);
            var found = Value(await client.NavigationAsync(new(runtime.SessionId, runId: run.RunId, stateKey: first.StateKey)));
            Assert.Equal(first.VisitId, Assert.Single(found.Visits).VisitId);
            view.Text.Text = "a different document with the same controls";
            run = Value(await client.NavigationAsync(Record(runtime, run, null, "open different content")));
            Assert.NotEqual(second.ObservationRevision, run.Visits[0].ObservationRevision);
            // Polling without an explicit action is not a new navigation loop or successful route.
            var previous = run.CurrentVisitId!;
            run = Value(await client.NavigationAsync(new(runtime.SessionId, "record", run.RunId, previousVisitId: previous)));
            var route = Value(await client.NavigationAsync(new(runtime.SessionId, runId: run.RunId, fromVisitId: previous, toVisitId: run.CurrentVisitId)));
            Assert.False(route.Route!.Complete); Assert.Equal("partial", route.Route.Status);
        });
    }

    [Fact]
    public async Task RedactionPrecedesRetentionAndChangingPolicyCannotRevealHistory()
    {
        await WithNavigation(async (runtime, view, target, client, _) =>
        {
            var secret = new string('x', 400) + "-private-token";
            view.Text.Text = secret; view.Context = "private-document";
            var policy = Policy(runtime, [secret, "private-document", "private-action"]);
            var run = Value(await client.NavigationAsync(new(runtime.SessionId, "start", maxNodes: 64, maxDepth: 8, identityTarget: target,
                visitLabel: "private-action", includeEvidence: true, policy: policy)));
            Assert.Null(run.Visits[0].Identity); Assert.Contains("host_identity_unavailable_or_redacted", run.Visits[0].Unavailable);
            run = Value(await client.NavigationAsync(new(runtime.SessionId, "record", run.RunId, identityTarget: target,
                previousVisitId: run.CurrentVisitId, transition: new("private-action", "succeeded", "private-action"), includeEvidence: true, policy: policy)));
            var json = JsonSerializer.Serialize(run);
            Assert.Contains(run.Visits[0].Observation!.Windows.SelectMany(item => item.Nodes), node => node.Text?.Contains("[REDACTED]", StringComparison.OrdinalIgnoreCase) == true);
            foreach (var text in new[] { secret[..300], "private-document", "private-action" }) Assert.DoesNotContain(text, json);
            Assert.Equal("navigation_policy_changed", (await client.NavigationAsync(new(runtime.SessionId, runId: run.RunId))).Error!.Code);
            var reread = Value(await client.NavigationAsync(new(runtime.SessionId, runId: run.RunId, includeEvidence: true, policy: policy)));
            Assert.DoesNotContain(secret[..300], JsonSerializer.Serialize(reread));
            AutomationProperties.SetAutomationId(view, "private-provider");
            var calls = view.IdentityCalls;
            var protectedPolicy = Policy(runtime, excluded: ["private-provider"]);
            var hidden = Value(await client.NavigationAsync(new(runtime.SessionId, "start", identityTarget: target, includeEvidence: true, policy: protectedPolicy)));
            Assert.Equal(calls, view.IdentityCalls); Assert.Null(hidden.Visits[0].Identity);
            Assert.DoesNotContain("private-token", JsonSerializer.Serialize(hidden));
            Assert.DoesNotContain(hidden.Visits[0].Observation!.Windows.SelectMany(item => item.Nodes), node => node.AutomationId == "private-provider");
            var denied = Policy(runtime, allowed: []);
            Assert.False((await client.NavigationAsync(new(runtime.SessionId, "start", policy: denied))).Success);
        });
    }

    [Fact]
    public async Task RetentionBoundsQueriesAndRoutesExposeEvictedEvidence()
    {
        await WithNavigation(async (runtime, _, _, _, _) =>
        {
            var run = Value(await runtime.NavigationAsync(new(runtime.SessionId, "start", maxNodes: 4, maxDepth: 1)));
            var first = run.CurrentVisitId!;
            for (var index = 0; index < 135; index++)
                run = Value(await runtime.NavigationAsync(Record(runtime, run, null, "revisit")));
            Assert.InRange(run.RetainedVisits, 1, 128); Assert.InRange(run.RetainedBytes, 1, 512 * 1024); Assert.True(run.DroppedVisits >= 8);
            Assert.Contains(run.Diagnostics, error => error.Code == "navigation_retention_gap");
            var oldest = Value(await runtime.NavigationAsync(new(runtime.SessionId, runId: run.RunId, visitId: first, includeEvidence: true)));
            Assert.Empty(oldest.Visits); Assert.Contains(oldest.Diagnostics, error => error.Code == "navigation_no_retained_match");
            var route = Value(await runtime.NavigationAsync(new(runtime.SessionId, runId: run.RunId, fromVisitId: first, toVisitId: run.CurrentVisitId)));
            Assert.False(route.Route!.Complete); Assert.Equal("outside_retention_or_invalid_order", route.Route.Status);
            var page = Value(await runtime.NavigationAsync(new(runtime.SessionId, runId: run.RunId, maxVisits: 1, includeEvidence: true)));
            Assert.Single(page.Visits); Assert.True(page.HasMore); Assert.NotNull(page.Visits[0].Observation);
            Assert.InRange(page.Loops.Count, 1, 32);
        });
    }

    [Fact]
    public async Task RunsExpireAndClearWhileProviderFailuresAndRestartPreserveIsolation()
    {
        await WithNavigation(async (runtime, view, target, client, _) =>
        {
            var run = Value(await client.NavigationAsync(new(runtime.SessionId, "start", ttlMs: 1500)));
            await Task.Delay(1600);
            Assert.Equal("navigation_run_unavailable", (await client.NavigationAsync(new(runtime.SessionId, runId: run.RunId))).Error!.Code);
            var runs = new List<RuntimeNavigationResponse>();
            for (var index = 0; index < 8; index++) runs.Add(Value(await runtime.NavigationAsync(new(runtime.SessionId, "start"))));
            Assert.Equal("navigation_run_limit", (await runtime.NavigationAsync(new(runtime.SessionId, "start"))).Error!.Code);
            Assert.Equal("cleared", Value(await client.NavigationAsync(new(runtime.SessionId, "clear", runs[0].RunId))).Status);
            view.BeforeIdentity = () => throw new InvalidOperationException("private-provider-exception");
            var failure = await client.NavigationAsync(new(runtime.SessionId, "start", identityTarget: target));
            // Existing debug-state providers report failure as unavailable identity; the sampled visit is still usable.
            Assert.True(failure.Success, JsonSerializer.Serialize(failure)); Assert.Null(failure.Value!.Visits[0].Identity);
            Assert.DoesNotContain("private-provider-exception", JsonSerializer.Serialize(failure));
            view.BeforeIdentity = null;
            var originalSession = runtime.SessionId;
            AvaScopeBridge.Deactivate();
            Assert.Equal("navigation_closed", (await runtime.NavigationAsync(new(originalSession, runId: runs[1].RunId))).Error!.Code);
            var next = AvaScopeBridge.Activate();
            Assert.Equal("navigation_session_mismatch", (await next.NavigationAsync(new(originalSession, runId: runs[1].RunId))).Error!.Code);
            Assert.Equal("navigation_run_unavailable", (await next.NavigationAsync(new(next.SessionId, runId: runs[1].RunId))).Error!.Code);
        });
    }

    [Fact]
    public async Task CliMcpAndObserversShareRetainedEvidenceAndRejectStaleIdentity()
    {
        await WithNavigation(async (runtime, view, target, client, _) =>
        {
            Assert.True((await client.SessionControlAsync(runtime.SessionId, new("acquire", "navigation-owner"))).Success);
            var outsider = new LocalBridgeClient(client.ManifestDirectory);
            var request = new RuntimeNavigationRequest(runtime.SessionId, "start", identityTarget: target);
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "navigation", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(arg);
                using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                var cli = JsonSerializer.Deserialize<ToolResult<RuntimeNavigationResponse>>(await stdout)!;
                Assert.True(cli.Success, await stdout + await stderr); Assert.Equal(0, process.ExitCode);
                view.Surface = "settings";
                var recorded = Value(await outsider.NavigationAsync(Record(runtime, cli.Value!, target, "open settings")));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
                { Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "navigation-test" }), cancellationToken: timeout.Token);
                var query = new RuntimeNavigationRequest(runtime.SessionId, runId: recorded.RunId, fromVisitId: cli.Value!.CurrentVisitId,
                    toVisitId: recorded.CurrentVisitId, includeEvidence: true);
                var call = await mcp.CallToolAsync("navigation", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(query), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var response = JsonSerializer.Deserialize<ToolResult<RuntimeNavigationResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(response.Success); Assert.Equal("reported_success", response.Value!.Route!.Status);
                Assert.All(response.Value.Visits, visit => Assert.NotNull(visit.Observation));
                var stale = new RuntimeTargetContext(target.SessionId, target.TopLevelId, TreeKinds.Visual, target.NodeId,
                    topLevelGeneration: target.TopLevelGeneration, nodeGeneration: "wrong-generation");
                Assert.Equal("navigation_identity_stale", (await outsider.NavigationAsync(Record(runtime, recorded, stale, "stale"))).Error!.Code);
                Assert.Equal(2, Value(await outsider.NavigationAsync(new(runtime.SessionId, runId: recorded.RunId))).RetainedVisits);
                Assert.Equal(0, view.Clicks);
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public void RequestContractsBoundScopeAndSeparateObservationFromActionReplay()
    {
        var session = new SessionId("session"); var id = Guid.NewGuid().ToString("N");
        Assert.Throws<ArgumentException>(() => new RuntimeNavigationRequest(session));
        Assert.Throws<ArgumentException>(() => new RuntimeNavigationRequest(session, "start", id));
        Assert.Throws<ArgumentException>(() => new RuntimeNavigationRequest(session, "record", id));
        Assert.Throws<ArgumentException>(() => new RuntimeNavigationRequest(session, runId: id, topLevelIds: ["window"]));
        Assert.Throws<ArgumentException>(() => new RuntimeNavigationRequest(session, runId: id, fromVisitId: id));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeNavigationRequest(session, "start", maxNodes: 65));
        Assert.Throws<ArgumentException>(() => new RuntimeNavigationAction("click", "guaranteed"));
        Assert.False(BridgeIpcMethods.RequiresControl(new("r", BridgeIpcMethods.Navigation, navigation: new(session, "start"))));
        foreach (var request in new[] { new RuntimeNavigationRequest(session, "start"), new(session, runId: id), new(session, "record", id, previousVisitId: id) })
            Assert.Equal(request.Action, JsonSerializer.Deserialize<RuntimeNavigationRequest>(JsonSerializer.Serialize(request))!.Action);
    }

    private static RuntimeNavigationRequest Record(AvaScopeBridgeRuntime runtime, RuntimeNavigationResponse run, RuntimeTargetContext? target, string action)
        => new(runtime.SessionId, "record", run.RunId, identityTarget: target, previousVisitId: run.CurrentVisitId, transition: new(action, "succeeded"));
    private static RuntimeNavigationResponse Value(CoreResult<RuntimeNavigationResponse> result)
    { Assert.True(result.Success, JsonSerializer.Serialize(result)); return result.Value!; }
    private static RuntimeEvidencePolicy Policy(AvaScopeBridgeRuntime runtime, IReadOnlyList<string>? redacted = null, IReadOnlyList<string>? excluded = null, IReadOnlyList<string>? allowed = null)
        => new(Path.Combine(Path.GetTempPath(), "avascope-navigation-tests"), redactedText: redacted, excludedControlAutomationIds: excluded,
            authorizedSessionIds: [runtime.SessionId.Value], allowedActions: allowed ?? [SemanticWorkflowActions.Inspect]);

    private static async Task WithNavigation(Func<AvaScopeBridgeRuntime, NavigationView, RuntimeTargetContext, LocalBridgeClient, Window, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var view = new NavigationView { Name = "NavigationRoot" };
                var window = new Window { Title = "Navigation sample", Width = 400, Height = 220, Content = view };
                try
                {
                    window.Show(); using var registered = runtime.RegisterTopLevel(window); Dispatcher.UIThread.RunJobs();
                    var top = Assert.Single(await runtime.ListTopLevelsAsync()).Id;
                    Assert.True((await runtime.ReadinessAsync(top, options: new(waitForFrame: true, timeoutMs: 5000))).Success);
                    var target = Assert.Single((await runtime.FindNodesAsync(top, TreeKinds.Visual, name: "NavigationRoot")).Value!.Matches).Target!;
                    await test(runtime, view, target, new(Path.GetDirectoryName(runtime.SessionManifestPath)!), window);
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    private sealed class NavigationView : UserControl, IAvaScopeDebugStateProvider
    {
        public string Surface { get; set; } = "home";
        public string Context { get; set; } = "document-a";
        public string Revision { get; set; } = "1";
        public TextBlock Text { get; } = new() { Text = "home" };
        public int Clicks { get; private set; }
        public int IdentityCalls { get; private set; }
        public Action? BeforeIdentity { get; set; }
        public NavigationView()
        {
            var button = new Button { Content = "Do not click while observing" }; button.Click += (_, _) => Clicks++;
            Content = new StackPanel { Children = { Text, button } };
        }
        public IReadOnlyDictionary<string, string?> GetAvaScopeDebugState()
        {
            IdentityCalls++; BeforeIdentity?.Invoke();
            return new Dictionary<string, string?> { ["navigation.surface"] = Surface, ["navigation.context"] = Context, ["navigation.revision"] = Revision };
        }
    }
}
