using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class WorkflowExporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "avascope-export-test-" + Guid.NewGuid().ToString("N"));
    private static readonly SemanticWorkflowTopLevelAlias MainAlias = new("main", new(title: "Export main"));

    [Fact]
    public async Task RecordedMultiWindowFlowReplaysOnFreshSessionAndReportsActualRegression()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await session.Dispatch(async () =>
            {
                var windows = new List<Window>();
                var registrations = new List<IDisposable>();
                TextBox? editor = null;
                var invoked = 0;
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate();
                try
                {
                    CreateWindows(false);
                    var originalSession = runtime.SessionId;
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath));
                    var source = new SemanticWorkflowRequest(runtime.SessionId, null,
                    [
                        new(SemanticWorkflowActions.TypeText, "edit", new(name: "Customer"), text: "${customer}", topLevelAlias: "main",
                            verify: new(new(SemanticWaitConditionKinds.Text, "${customer}"))),
                        new(SemanticWorkflowActions.Invoke, "open", new(name: "Open"), topLevelAlias: "main",
                            verify: new(new(SemanticWaitConditionKinds.TopLevelOpened, topLevelTitle: "Export detail"), captureBefore: false)),
                        new(SemanticWorkflowActions.Invoke, "complete", new(name: "Complete"), topLevelAlias: "detail",
                            verify: new(new(SemanticWaitConditionKinds.Text, "Ready"), new(name: "Status"), timeoutMs: 1500, pollIntervalMs: 25))
                    ], outputDirectory: Path.Combine(_root, "record"),
                        topLevelAliases: [MainAlias, new("detail", new(title: "Export detail"))],
                        variables: new Dictionary<string, string> { ["customer"] = "alice-private" },
                        evidence: new(includeScreenshot: false, policy: new(_root, redactedText: ["alice-private"],
                            allowedActions: [SemanticWorkflowActions.TypeText, SemanticWorkflowActions.Invoke])));
                    var recorded = await new SemanticWorkflowRunner().RunAsync(client, source);
                    Assert.True(recorded.Success, recorded.Error?.Message);
                    Assert.Equal("passed", recorded.Value!.Status);
                    var request = new WorkflowExportRequest(source, recorded.Value, _root,
                        new Dictionary<string, string> { ["customer"] = "alice-private" });
                    var exported = AvaScopeMcpTools.ExportWorkflow(request);
                    Assert.True(exported.Success, exported.Error?.Message);
                    Assert.Equal("ready", exported.Value!.Status);
                    Assert.True(exported.Value.Validated);
                    Assert.Equal(3, exported.Value.VerifiedStepIds.Count);
                    Assert.Single(exported.Value.Parameters);
                    foreach (var file in new[] { exported.Value.ExportPath, exported.Value.WorkflowPath })
                    {
                        var json = await File.ReadAllTextAsync(file);
                        Assert.DoesNotContain("alice-private", json);
                        Assert.DoesNotContain(originalSession.Value, json);
                    }

                    CleanWindows();
                    AvaScopeBridge.Deactivate();
                    runtime = AvaScopeBridge.Activate();
                    Assert.NotEqual(originalSession, runtime.SessionId);
                    CreateWindows(false);
                    client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath));
                    var replay = new WorkflowReplayRequest(exported.Value.ExportPath, runtime.SessionId, Path.Combine(_root, "replay"),
                        new Dictionary<string, string> { ["export_customer"] = "bob-private" }, EvidenceRoot: _root);
                    var validated = await AvaScopeMcpTools.ReplayWorkflow(client, replay);
                    Assert.True(validated.Success, validated.Error?.Message);
                    Assert.Equal("validated", validated.Value!.Status);
                    Assert.Equal(1, invoked);
                    Assert.True(string.IsNullOrEmpty(editor!.Text));
                    var passed = await AvaScopeMcpTools.ReplayWorkflow(client, replay with { ValidateOnly = false });
                    Assert.True(passed.Success, passed.Error?.Message);
                    Assert.Equal("passed", passed.Value!.Status);
                    Assert.Equal("bob-private", editor.Text);
                    Assert.Equal(2, invoked);

                    CleanWindows();
                    AvaScopeBridge.Deactivate();
                    runtime = AvaScopeBridge.Activate();
                    CreateWindows(true);
                    client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath));
                    var failing = replay with { SessionId = runtime.SessionId, OutputDirectory = Path.Combine(_root, "regression"), ValidateOnly = false };
                    var failed = await WorkflowExporter.ReplayAsync(client, failing);
                    Assert.True(failed.Success, failed.Error?.Message);
                    Assert.Equal("failed", failed.Value!.Status);
                    Assert.NotNull(failed.Value.ReportPack);
                    Assert.Contains(failed.Value.Steps, step => step.Verification?.Status == "failed");
                    Assert.DoesNotContain("bob-private", JsonSerializer.Serialize(failed.Value));
                    foreach (var file in Directory.GetFiles(failed.Value.ReportPack.ReportDirectory))
                        Assert.DoesNotContain("bob-private", await File.ReadAllTextAsync(file));

                    // Both CLI adapters consume the same DTOs. Validation cannot invoke the application.
                    var cliExport = await Cli<WorkflowExportResponse>("export-workflow", request);
                    Assert.True(cliExport.Result.Success, cliExport.Result.Error?.Message);
                    var cliReplay = await Cli<SemanticWorkflowResponse>("replay-workflow", replay with { ExportPath = cliExport.Result.Value!.ExportPath });
                    Assert.Equal(0, cliReplay.Exit);
                    Assert.Equal("validated", cliReplay.Result.Value!.Status);
                    Assert.Equal(3, invoked);
                }
                finally { CleanWindows(); AvaScopeBridge.Deactivate(); }

                void CreateWindows(bool regression)
                {
                    editor = new TextBox { Name = "Customer" };
                    var open = new Button { Name = "Open", Content = "Open" };
                    var main = new Window { Title = "Export main", Width = 360, Height = 240, Content = new StackPanel { Children = { editor, open } } };
                    open.Click += (_, _) =>
                    {
                        var status = new TextBlock { Name = "Status", Text = "Waiting" };
                        var complete = new Button { Name = "Complete", Content = "Complete" };
                        complete.Click += async (_, _) => { invoked++; await Task.Delay(80); status.Text = regression ? "Broken" : "Ready"; };
                        var detail = new Window { Title = "Export detail", Width = 300, Height = 200, Content = new StackPanel { Children = { status, complete } } };
                        windows.Add(detail);
                        detail.Show();
                        registrations.Add(runtime.RegisterTopLevel(detail));
                    };
                    windows.Add(main);
                    main.Show();
                    registrations.Add(runtime.RegisterTopLevel(main));
                }
                void CleanWindows()
                {
                    foreach (var registration in registrations) registration.Dispose();
                    registrations.Clear();
                    foreach (var window in windows) window.Close();
                    windows.Clear();
                }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    [Fact]
    public async Task UnstableTargetsUnpairedInputsAndMissingAssertionsAreNotPromotedToSuccessfulTests()
    {
        var source = Source(
            new(SemanticWorkflowActions.Invoke, "transient", new(nodeId: "old-node"), topLevelAlias: "main"),
            new(SemanticWorkflowActions.Wait, "delay", waitMs: 10, topLevelAlias: "main"),
            new(SemanticWorkflowActions.KeyDown, "held", new(name: "Customer"), key: "A", topLevelAlias: "main"));
        var exported = WorkflowExporter.Export(new(source, Recording(source), _root));
        Assert.True(exported.Success, exported.Error?.Message);
        Assert.Equal("needs_review", exported.Value!.Status);
        Assert.Empty(exported.Value.VerifiedStepIds);
        Assert.Contains(exported.Value.ReviewItems, item => item.Code == "missing_verified_postcondition");
        Assert.Contains(exported.Value.ReviewItems, item => item.Code == "fixed_sleep");
        Assert.Contains(exported.Value.ReviewItems, item => item.Code == "unstable_selector" && item.BlocksReplay);
        Assert.Contains(exported.Value.ReviewItems, item => item.Code == "unpaired_input" && item.BlocksReplay);
        Assert.DoesNotContain("old-node", File.ReadAllText(exported.Value.WorkflowPath));
        var denied = await WorkflowExporter.ReplayAsync(new LocalBridgeClient(), new(exported.Value.ExportPath,
            new("fresh"), _root, AcknowledgeReview: true));
        Assert.Equal("workflow_export_review_required", denied.Error!.Code);
    }

    [Fact]
    public async Task ExportRequiresOriginalExecutionAndExplicitReviewAndParameterBindings()
    {
        var source = Source(new SemanticWorkflowStep(SemanticWorkflowActions.TypeText, "edit", new(name: "Customer"), text: "private-value", topLevelAlias: "main"));
        var other = Source(new SemanticWorkflowStep(SemanticWorkflowActions.TypeText, "edit", new(name: "Other"), text: "private-value", topLevelAlias: "main"));
        Assert.Equal("workflow_export_definition_mismatch", WorkflowExporter.Export(new(other, Recording(source), _root)).Error!.Code);
        var validation = await new SemanticWorkflowRunner().RunAsync(new LocalBridgeClient(), new(source.SessionId, null, source.Steps,
            requestId: source.RequestId, topLevelAliases: source.TopLevelAliases, validateOnly: true));
        Assert.Equal("workflow_export_definition_mismatch", WorkflowExporter.Export(new(source, validation.Value!, _root)).Error!.Code);
        var exported = WorkflowExporter.Export(new(source, Recording(source), _root));
        Assert.True(exported.Success, exported.Error?.Message);
        Assert.Empty(exported.Value!.VerifiedStepIds);
        Assert.DoesNotContain("private-value", File.ReadAllText(exported.Value.ExportPath));
        var replay = new WorkflowReplayRequest(exported.Value.ExportPath, new("fresh"), _root);
        Assert.Equal("workflow_export_review_required", (await WorkflowExporter.ReplayAsync(new(), replay)).Error!.Code);
        Assert.Equal("workflow_export_parameters_required", (await WorkflowExporter.ReplayAsync(new(), replay with { AcknowledgeReview = true })).Error!.Code);
        var bound = replay with { AcknowledgeReview = true, Parameters = new Dictionary<string, string> { [exported.Value.Parameters.Single().Name] = "new-value" } };
        var valid = await WorkflowExporter.ReplayAsync(new(), bound);
        Assert.True(valid.Success, valid.Error?.Message);
        Assert.Equal("validated", valid.Value!.Status);
    }

    [Fact]
    public async Task RedactionOverlapsDoNotCorruptParameterTokensOrRelaxActionPolicies()
    {
        var source = new SemanticWorkflowRequest(new("recorded"), null,
            [new(SemanticWorkflowActions.TypeText, "edit", new(name: "Customer"), text: "export_value", topLevelAlias: "main")],
            topLevelAliases: [MainAlias], evidence: new(policy: new(_root, redactedText: ["export_value", "value"])), requestId: "recorded");
        var exported = WorkflowExporter.Export(new(source, Recording(source), _root,
            new Dictionary<string, string> { ["long"] = "export_value", ["short"] = "value" }));
        Assert.True(exported.Success, exported.Error?.Message);
        Assert.Contains(exported.Value!.ReviewItems, item => item.Code == "action_policy_denied" && item.BlocksReplay);
        var text = File.ReadAllText(exported.Value.WorkflowPath);
        Assert.Contains("${export_long}", text);
        Assert.DoesNotContain("${export_${", text);
        var denied = await WorkflowExporter.ReplayAsync(new(), new(exported.Value.ExportPath, new("fresh"), _root,
            new Dictionary<string, string> { ["export_long"] = "new-private", ["export_short"] = "other-private" },
            AcknowledgeReview: true, EvidenceRoot: _root));
        Assert.Equal("workflow_export_review_required", denied.Error!.Code);
    }

    [Fact]
    public async Task MissingWindowIdentityProducesBlockedDraftAndPolicyAuthorizationMustBeRebound()
    {
        var transient = new SemanticWorkflowRequest(new("recorded"), "old-window",
            [new(SemanticWorkflowActions.Inspect, selector: new(name: "Customer"))]);
        var draft = WorkflowExporter.Export(new(transient, Recording(transient), _root));
        Assert.True(draft.Success, draft.Error?.Message);
        Assert.False(draft.Value!.Validated);
        Assert.Contains(draft.Value.ReviewItems, item => item.Code == "stable_window_alias_required" && item.BlocksReplay);
        Assert.DoesNotContain("old-window", File.ReadAllText(draft.Value.WorkflowPath));
        var source = new SemanticWorkflowRequest(new("recorded"), null,
            [new(SemanticWorkflowActions.Inspect, selector: new(name: "Customer"), topLevelAlias: "main")],
            topLevelAliases: [MainAlias], isolatedStateDirectory: Path.Combine(_root, "old-state"),
            evidence: new(policy: new(_root, authorizedSessionIds: ["recorded"], authorizedProcessIds: [Environment.ProcessId])));
        var exported = WorkflowExporter.Export(new(source, Recording(source), _root));
        Assert.True(exported.Success, exported.Error?.Message);
        var replay = new WorkflowReplayRequest(exported.Value!.ExportPath, new("fresh"), Path.Combine(_root, "new-run"), AcknowledgeReview: true);
        Assert.Equal("workflow_export_authorization_required", (await WorkflowExporter.ReplayAsync(new(), replay)).Error!.Code);
        var bound = await WorkflowExporter.ReplayAsync(new(), replay with
        {
            EvidenceRoot = _root, AuthorizedProcessId = Environment.ProcessId,
            IsolatedStateDirectory = Path.Combine(_root, "new-state")
        });
        Assert.True(bound.Success, bound.Error?.Message);
        Assert.Equal("validated", bound.Value!.Status);
        Assert.Equal("workflow_replay_invalid", (await WorkflowExporter.ReplayAsync(new(), replay with { SessionId = null! })).Error!.Code);
    }

    [Fact]
    public async Task ExportRebindsActivationGeometryWithoutDiscardingTheDispatchGuard()
    {
        var oldRevision = new string('a', 64);
        var source = Source(new SemanticWorkflowStep(SemanticWorkflowActions.Click, "save", new(name: "Save"), topLevelAlias: "main",
            inputExecution: new() { ExpectedGeometryRevision = oldRevision }));
        var exported = WorkflowExporter.Export(new(source, Recording(source), _root));
        Assert.True(exported.Success, exported.Error?.Message);
        Assert.True(exported.Value!.Validated);
        Assert.Contains(exported.Value.ReviewItems, item => item.Code == "activation_geometry_rebind");
        Assert.DoesNotContain(oldRevision, File.ReadAllText(exported.Value.ExportPath));
        Assert.Contains("expectedGeometryRevision", File.ReadAllText(exported.Value.WorkflowPath));
        var parameter = Assert.Single(exported.Value.Parameters);
        var replay = new WorkflowReplayRequest(exported.Value.ExportPath, new("fresh"), _root, AcknowledgeReview: true);
        Assert.Equal("workflow_export_parameters_required", (await WorkflowExporter.ReplayAsync(new(), replay)).Error!.Code);
        var valid = await WorkflowExporter.ReplayAsync(new(), replay with
        {
            Parameters = new Dictionary<string, string> { [parameter.Name] = new string('b', 64) }
        });
        Assert.True(valid.Success, valid.Error?.Message);
        Assert.Equal("validated", valid.Value!.Status);
        var invalid = await WorkflowExporter.ReplayAsync(new(), replay with
        {
            Parameters = new Dictionary<string, string> { [parameter.Name] = "invalid" }
        });
        Assert.False(invalid.Value!.Plan!.Valid);
        Assert.Contains(invalid.Value.Diagnostics, error => error.Code == "semantic_workflow_input_execution_invalid");
    }

    [Fact]
    public async Task ExportRetainsRelationshipsWhileRemovingTheirTransientNodeIds()
    {
        var source = Source(new SemanticWorkflowStep(SemanticWorkflowActions.Invoke, "edit",
            new(nodeType: "Button", relationships: [new("ancestor", new(nodeId: "visual:old-container", name: "Shipping"))]), topLevelAlias: "main"));
        var exported = WorkflowExporter.Export(new(source, Recording(source), _root));
        Assert.True(exported.Success, exported.Error?.Message);
        Assert.True(exported.Value!.Validated);
        var text = File.ReadAllText(exported.Value.WorkflowPath);
        Assert.DoesNotContain("visual:old-container", text);
        Assert.Contains("Shipping", text);
        Assert.Contains("ancestor", text);
        var replay = await WorkflowExporter.ReplayAsync(new(), new(exported.Value.ExportPath, new("fresh"), _root, AcknowledgeReview: true));
        Assert.Equal("validated", replay.Value!.Status);
    }

    private static SemanticWorkflowRequest Source(params SemanticWorkflowStep[] steps) => new(new("recorded"), null, steps,
        requestId: "recorded", topLevelAliases: [MainAlias]);

    private static SemanticWorkflowResponse Recording(SemanticWorkflowRequest source) => new(source.RequestId, source.SessionId,
        source.TopLevelId, "passed", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
        source.Steps.Select(step => new SemanticWorkflowStepResult(step.Id, step.Action, "passed", "Dispatched.", DateTimeOffset.UtcNow)).ToArray(),
        metadata: new Dictionary<string, string> { ["requestDefinitionSha256"] = SemanticWorkflowRunner.DefinitionHash(source) });

    private async Task<(int Exit, ToolResult<T> Result)> Cli<T>(string command, object request)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, command + ".json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), command, "--request", path }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var result = JsonSerializer.Deserialize<ToolResult<T>>(await output);
        Assert.NotNull(result);
        Assert.True(string.IsNullOrEmpty(await error), await error);
        return (process.ExitCode, result);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
