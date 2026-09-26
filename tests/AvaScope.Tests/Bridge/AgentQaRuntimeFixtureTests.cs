using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.ComplexWorkflowApp;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class AgentQaRuntimeFixtureTests
{
    [Theory]
    [InlineData(1120, 800)]
    [InlineData(680, 620)]
    public async Task DocumentNavigationUsesRealActionsAndSeparatesContextRevisionResetAndPrivacy(int width, int height)
    {
        var directory = Path.Combine(Path.GetTempPath(), "avascope-qa-navigation-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable("AVASCOPE_QA_OUTPUT");
        Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", directory);
        try
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate();
                var window = new QaWindow { Width = width, Height = height }; window.Show();
                try
                {
                    using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    window.FindControl<TabControl>("Pages")!.SelectedIndex = 8;
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
                    Assert.True((await runtime.ReadinessAsync(top.Id, options: new(waitForFrame: true, timeoutMs: 5000))).Success);
                    var view = window.FindControl<QaNavigationView>("NavigationFixture")!;
                    var identity = Assert.Single((await runtime.FindNodesAsync(top.Id, TreeKinds.Visual,
                        automationId: "qa-navigation-identity", maxDepth: 32)).Value!.Matches).Target!;
                    async Task Invoke(string automationId)
                    {
                        var target = Assert.Single((await runtime.FindNodesAsync(top.Id, TreeKinds.Visual,
                            automationId: automationId, maxDepth: 32)).Value!.Matches).Target!;
                        var input = await client.InputAsync(runtime.SessionId, top.Id, InputActions.Invoke, inputTarget: target);
                        Assert.True(input.Success && input.Value!.Handled, JsonSerializer.Serialize(input));
                        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    }
                    async Task<RuntimeNavigationResponse> Observe(RuntimeNavigationResponse? prior = null)
                    {
                        var result = await client.NavigationAsync(new(runtime.SessionId, prior is null ? "start" : "record",
                            runId: prior?.RunId, identityTarget: identity, previousVisitId: prior?.CurrentVisitId,
                            transition: prior is null ? null : new("Fixture action completed", "succeeded")));
                        Assert.True(result.Success, JsonSerializer.Serialize(result));
                        var visit = result.Value!.Visits[0];
                        Assert.Equal(new RuntimeNavigationIdentity(view.Surface, view.Context, view.Revision), visit.Identity);
                        Assert.Contains("hidden_state_unverified", visit.Equivalence);
                        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                        var journal = state.RootElement.GetProperty("navigation");
                        Assert.Equal(visit.Identity!.Context, journal.GetProperty("context").GetString());
                        Assert.Equal(visit.Identity.Surface, journal.GetProperty("surface").GetString());
                        Assert.Equal(visit.Identity.Revision, journal.GetProperty("revision").GetString());
                        Assert.Contains(view.Context, window.FindControl<TextBlock>("NavigationContent")!.Text);
                        Assert.Equal(0, state.RootElement.GetProperty("form").GetProperty("saves").GetInt32());
                        return result.Value;
                    }
                    var run = await Observe(); var first = run.Visits[0];
                    await Invoke("qa-navigation-details"); run = await Observe(run);
                    Assert.Equal("details", view.Surface); Assert.Equal(first.Identity!.Revision, view.Revision);
                    Assert.NotEqual(first.StateKey, run.Visits[0].StateKey);
                    await Invoke("qa-navigation-overview"); run = await Observe(run);
                    Assert.Equal(first.StateKey, run.Visits[0].StateKey);
                    Assert.NotEqual(first.VisitId, run.Visits[0].VisitId);
                    Assert.Contains(run.Loops, loop => loop.Length == 2 && loop.Confidence == "host_declared_and_sampled_revisit");
                    await Invoke("qa-navigation-document"); run = await Observe(run);
                    var documentB = run.Visits[0]; Assert.Equal("document-b", view.Context);
                    Assert.NotEqual(first.StateKey, documentB.StateKey);
                    await Invoke("qa-navigation-edit"); run = await Observe(run);
                    var edited = run.Visits[0]; Assert.Equal(1, view.DocumentRevision);
                    Assert.NotEqual(documentB.StateKey, edited.StateKey);
                    await Invoke("qa-navigation-document"); run = await Observe(run);
                    Assert.Equal(0, view.DocumentRevision); Assert.Equal(first.StateKey, run.Visits[0].StateKey);
                    await Invoke("qa-navigation-document"); run = await Observe(run);
                    Assert.Equal(1, view.DocumentRevision); Assert.Equal(edited.StateKey, run.Visits[0].StateKey);

                    var policy = new RuntimeEvidencePolicy(directory, excludedControlAutomationIds: ["qa-navigation-identity"],
                        authorizedSessionIds: [runtime.SessionId.Value], allowedActions: [SemanticWorkflowActions.Inspect]);
                    var privateRun = await client.NavigationAsync(new(runtime.SessionId, "start", identityTarget: identity, policy: policy));
                    Assert.True(privateRun.Success, JsonSerializer.Serialize(privateRun));
                    Assert.Null(privateRun.Value!.Visits[0].Identity);
                    Assert.Contains("identity_excluded_by_policy", privateRun.Value.Visits[0].Unavailable);
                    Assert.Equal("navigation_policy_changed", (await client.NavigationAsync(new(runtime.SessionId,
                        runId: privateRun.Value.RunId))).Error!.Code);

                    var generation = view.Generation;
                    await Invoke("qa-reset");
                    Assert.Equal(generation + 1, view.Generation); Assert.Equal(0, view.DocumentRevision);
                    Assert.Equal("overview", view.Surface); Assert.Equal("document-a", view.Context);
                    window.FindControl<TabControl>("Pages")!.SelectedIndex = 8;
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    identity = Assert.Single((await runtime.FindNodesAsync(top.Id, TreeKinds.Visual,
                        automationId: "qa-navigation-identity", maxDepth: 32)).Value!.Matches).Target!;
                    run = await Observe(run); Assert.NotEqual(first.StateKey, run.Visits[0].StateKey);
                    Assert.NotEqual(first.Identity.Revision, run.Visits[0].Identity!.Revision);
                    Assert.True((await client.NavigationAsync(new(runtime.SessionId, "clear", run.RunId))).Success);
                    Assert.Equal("navigation_run_unavailable", (await client.NavigationAsync(new(runtime.SessionId, runId: run.RunId))).Error!.Code);
                    Assert.True((await client.NavigationAsync(new(runtime.SessionId, "clear", privateRun.Value.RunId, policy: policy))).Success);
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", previous);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(1120, 800)]
    [InlineData(680, 620)]
    public async Task DrawnRecordsUseTransformedPointerCoordinatesAndDiagnosticsResetToCleanState(int width, int height)
    {
        var directory = Path.Combine(Path.GetTempPath(), "avascope-qa-scene-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable("AVASCOPE_QA_OUTPUT");
        Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", directory);
        try
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
            await BridgeHeadlessSmokeTests.DispatchAsync(session, () =>
            {
                var window = new QaWindow(); window.Show();
                try
                {
                    var scene = window.FindControl<QaSceneControl>("RuntimeScene")!;
                    var toggle = window.FindControl<CheckBox>("DiagnosticToggle")!;
                    var editor = window.FindControl<TextBox>("DiagnosticEditor")!;
                    var binding = window.FindControl<TextBlock>("DiagnosticBinding")!;
                    var layout = window.FindControl<TextBlock>("DiagnosticLayout")!;
                    for (var cycle = 0; cycle < 2; cycle++)
                    {
                        window.Width = width; window.Height = height;
                        window.FindControl<TabControl>("Pages")!.SelectedIndex = 7;
                        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                        Assert.False(DataValidationErrors.GetHasErrors(editor));
                        Assert.Equal("Binding fixture disabled", binding.Text);
                        Assert.Equal(260, layout.Width);
                        Assert.True(scene.Bounds.Width >= 550, $"The shifted scene is clipped: {scene.Bounds}.");
                        var orange = scene.TranslatePoint(scene.Items[1].Bounds.Center, window)!.Value;
                        window.MouseDown(orange, MouseButton.Left); window.MouseUp(orange, MouseButton.Left);
                        Assert.Equal("orange", scene.SelectedId);
                        scene.Shift();
                        var green = scene.TranslatePoint(scene.Items[2].Bounds.Center + new Vector(20, 0), window)!.Value;
                        window.MouseDown(green, MouseButton.Left); window.MouseUp(green, MouseButton.Left);
                        Assert.Equal("green", scene.SelectedId); Assert.Equal(2, scene.SelectionCount);
                        var revision = scene.Revision;
                        Assert.False(scene.Select("absent")); Assert.Equal(revision, scene.Revision);
                        toggle.IsChecked = true; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                        Assert.True(DataValidationErrors.GetHasErrors(editor));
                        Assert.Equal("Expected missing binding", binding.Text);
                        Assert.Equal(800, layout.Width); Assert.True(layout.Bounds.Width > Assert.IsAssignableFrom<Control>(layout.Parent).Bounds.Width);
                        using (var journal = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json"))))
                        {
                            Assert.Equal("green", journal.RootElement.GetProperty("scene").GetProperty("selectedId").GetString());
                            Assert.False(journal.RootElement.GetProperty("scene").GetProperty("declared").GetBoolean());
                            Assert.True(journal.RootElement.GetProperty("intentionalDiagnostics").GetProperty("validationError").GetBoolean());
                        }
                        toggle.IsChecked = false; Dispatcher.UIThread.RunJobs();
                        Assert.False(DataValidationErrors.GetHasErrors(editor));
                        Assert.Equal("Binding fixture disabled", binding.Text);
                        toggle.IsChecked = true;
                        var generation = scene.Generation;
                        window.ResetState(); Dispatcher.UIThread.RunJobs();
                        Assert.Equal(generation + 1, scene.Generation);
                        Assert.Null(scene.SelectedId); Assert.Equal(0, scene.SelectionCount); Assert.Equal(0, scene.Offset);
                        Assert.False(DataValidationErrors.GetHasErrors(editor));
                        Assert.Equal("Binding fixture disabled", binding.Text); Assert.Equal(260, layout.Width);
                        Assert.Equal(new[] { "blue", "orange", "green" }, scene.Items.Select(item => item.Id));
                    }
                }
                finally { window.Close(); }
                return Task.CompletedTask;
            }, CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", previous);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task RealHostDeclarationsRejectStaleSceneObjectsAndWorkCannotOverwriteResetOrCleanup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avascope-qa-declarations-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable("AVASCOPE_QA_OUTPUT");
        Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", directory);
        try
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate(new(enableCustomActions: true,
                    allowedCustomActions: [QaRuntimeRegistration.SelectAction, QaRuntimeRegistration.WorkAction]));
                var window = new QaWindow(); window.Show();
                IReadOnlyList<IDisposable> declarations = [];
                try
                {
                    using var registration = runtime.RegisterTopLevel(window);
                    declarations = QaRuntimeRegistration.Register(runtime, window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    window.FindControl<TabControl>("Pages")!.SelectedIndex = 7;
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    var scene = window.FindControl<QaSceneControl>("RuntimeScene")!;
                    var target = Assert.Single((await runtime.FindNodesAsync(top.Id, TreeKinds.Visual,
                        automationId: "qa-scene", maxDepth: 32)).Value!.Matches).Target!;
                    var snapshot = await client.SceneAsync(new(target));
                    Assert.True(OperationResultMapper.IsSuccessful(snapshot), JsonSerializer.Serialize(snapshot));
                    Assert.Equal(3, snapshot.Value!.Snapshot!.Objects.Count);
                    var old = Assert.Single(snapshot.Value.Snapshot.Objects, item => item.Object.Id == "orange").Target!;
                    var selected = await client.SceneAsync(new(target, "invoke", expectedObject: old,
                        actionName: QaRuntimeRegistration.SelectAction, requestId: "scene-select"));
                    Assert.True(OperationResultMapper.IsSuccessful(selected), JsonSerializer.Serialize(selected));
                    Assert.Equal("orange", scene.SelectedId); Assert.Equal(1, scene.SelectionCount);
                    scene.Shift();
                    var beforeRefusal = File.ReadAllText(Path.Combine(directory, "qa-state.json"));
                    var stale = OperationResultMapper.ToToolResult(await client.SceneAsync(new(target, "invoke", expectedObject: old,
                        actionName: QaRuntimeRegistration.SelectAction, requestId: "stale-scene")));
                    Assert.Equal("scene_stale", stale.Error!.Code);
                    Assert.Equal(beforeRefusal, File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                    var fresh = (await client.SceneAsync(new(target, objectId: "orange"))).Value!.Snapshot!.Objects[0].Target!;
                    window.ResetState(); window.FindControl<TabControl>("Pages")!.SelectedIndex = 7;
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    stale = OperationResultMapper.ToToolResult(await client.SceneAsync(new(target, "invoke", expectedObject: fresh,
                        actionName: QaRuntimeRegistration.SelectAction, requestId: "reset-scene")));
                    Assert.Equal("scene_stale", stale.Error!.Code); Assert.Null(scene.SelectedId);

                    var work = Assert.Single((await runtime.FindNodesAsync(top.Id, TreeKinds.Visual,
                        automationId: "qa-operation-start", maxDepth: 32)).Value!.Matches).Target!;
                    var action = Assert.Single((await client.CustomActionsAsync(runtime.SessionId, work)).Value!.Actions);
                    Assert.True(action.SupportsOperations); Assert.True(action.SupportsCancellation);
                    Assert.Equal(RuntimeCustomActionParameterTypes.Integer, Assert.Single(action.Parameters, parameter => parameter.Name == "steps").Type);
                    async Task<RuntimeCustomActionResponse> Start(string mode, string steps)
                    {
                        var result = await client.InvokeCustomActionAsync(runtime.SessionId, new(Guid.NewGuid().ToString("N"), work,
                            QaRuntimeRegistration.WorkAction, parameters: new Dictionary<string, string> { ["mode"] = mode, ["steps"] = steps }));
                        Assert.True(result.Success, result.Error?.Message); return result.Value!;
                    }
                    var completed = (await Start("complete", "3")).Operation!;
                    var complete = await client.OperationAsync(new(runtime.SessionId, completed.OperationId, "wait", 3000));
                    Assert.Equal("completed", complete.Value!.Operation.Status);
                    Assert.Equal(1, complete.Value.Operation.Progress); Assert.Equal("3", complete.Value.Operation.Result["steps"]);
                    Assert.Equal(100, window.FindControl<ProgressBar>("OperationProgress")!.Value);
                    var failed = (await Start("fail", "1")).Operation!;
                    var failure = await client.OperationAsync(new(runtime.SessionId, failed.OperationId, "wait", 3000));
                    Assert.Equal("failed", failure.Value!.Operation.Status); Assert.Equal("qa_deliberate_failure", failure.Value.Operation.Error!.Code);
                    var cancelled = (await Start("complete", "10")).Operation!;
                    Assert.True((await client.OperationAsync(new(runtime.SessionId, cancelled.OperationId, "cancel"))).Success);
                    Assert.Equal("cancelled", (await client.OperationAsync(new(runtime.SessionId, cancelled.OperationId, "wait", 3000))).Value!.Operation.Status);
                    var beforeInvalid = File.ReadAllText(Path.Combine(directory, "qa-state.json"));
                    foreach (var steps in new[] { "0", "11", "2147483648" })
                    {
                        var invalid = await Start("complete", steps);
                        Assert.Equal("failed", invalid.Status); Assert.Null(invalid.Operation);
                    }
                    Assert.Equal(beforeInvalid, File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                    var resetWork = (await Start("complete", "10")).Operation!;
                    window.ResetState();
                    var reset = File.ReadAllText(Path.Combine(directory, "qa-state.json"));
                    Assert.Equal("cancelled", (await client.OperationAsync(new(runtime.SessionId, resetWork.OperationId, "wait", 3000))).Value!.Operation.Status);
                    Assert.Equal(reset, File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                    using (var journal = JsonDocument.Parse(reset))
                    {
                        Assert.Equal("idle", journal.RootElement.GetProperty("operation").GetProperty("state").GetString());
                        Assert.Equal(0, journal.RootElement.GetProperty("operation").GetProperty("starts").GetInt32());
                    }
                    window.FindControl<TabControl>("Pages")!.SelectedIndex = 7;
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    var cleanupWork = (await Start("complete", "10")).Operation!;
                    window.CleanupState();
                    var cleanup = File.ReadAllText(Path.Combine(directory, "qa-state.json"));
                    Assert.Equal("cancelled", (await client.OperationAsync(new(runtime.SessionId, cleanupWork.OperationId, "wait", 3000))).Value!.Operation.Status);
                    Assert.Equal(cleanup, File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                }
                finally
                {
                    window.Close();
                    foreach (var declaration in declarations) declaration.Dispose();
                    AvaScopeBridge.Deactivate();
                }
            }, CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", previous);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
