using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeRelationshipQueryTests
{
    [Fact]
    public async Task PopupLogicalIdentityIsUniqueAcrossSnapshotsQueriesCliAndMcp()
    {
        await WithWindow(async (runtime, window, root, top, client) =>
        {
            var open = new Button { Content = "Open" };
            var close = new Button { Content = "Dismiss" };
            AutomationProperties.SetAutomationId(close, "popup-close");
            var popup = new Popup { PlacementTarget = open, Child = new Border { Child = close } };
            root.Children.Add(new TabControl { Items =
            {
                new TabItem { Header = "Other", Content = new TextBlock { Text = "Other page" } },
                new TabItem { Header = "Popup", Content = new StackPanel { Children = { open, popup } } }
            }, SelectedIndex = 1 });
            var path = Path.Combine(Path.GetTempPath(), "avascope-popup-query-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var request = new RuntimeQueryRequest(runtime.SessionId, top, new(treeKind: TreeKinds.Logical, automationId: "popup-close"), maxDepth: 32);
                foreach (var isOpen in new[] { false, true, false, true })
                {
                    popup.IsOpen = isOpen; window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    var flat = await client.FindNodesAsync(runtime.SessionId, top, TreeKinds.Logical, automationId: "popup-close", maxDepth: 32);
                    Assert.True(flat.Success, flat.Error?.Message);
                    var match = Assert.Single(flat.Value!.Matches);
                    var query = await client.QueryNodesAsync(request);
                    Assert.True(query.Success, query.Error?.Message);
                    Assert.True(query.Value!.Coverage!.Complete);
                    Assert.Equal(match.Node.NodeId, Assert.Single(query.Value.Matches).Node.NodeId);
                    Assert.Equal(match.Path, query.Value.Matches[0].Path);
                    var snapshot = await client.LogicalTreeAsync(runtime.SessionId, top, maxDepth: 32);
                    Assert.True(snapshot.Success, snapshot.Error?.Message);
                    var pending = new Stack<TreeNodeSummary>(); pending.Push(snapshot.Value!.Root);
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    while (pending.TryPop(out var node))
                    {
                        Assert.True(ids.Add(node.NodeId), "One logical object appeared twice in a tree snapshot.");
                        foreach (var child in node.Children) pending.Push(child);
                    }
                }
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "find-nodes", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token); var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                try { await process.WaitForExitAsync(timeout.Token); }
                finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                Assert.Equal(0, process.ExitCode);
                var cli = JsonSerializer.Deserialize<ToolResult<FindNodesResponse>>(await output)!;
                Assert.True(cli.Success, cli.Error?.Message + await errors); Assert.Single(cli.Value!.Matches);
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
                { Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "popup-logical-query" }), cancellationToken: timeout.Token);
                var call = await mcp.CallToolAsync("find_nodes", new Dictionary<string, object?>
                {
                    ["sessionId"] = runtime.SessionId.Value, ["topLevelId"] = top,
                    ["selector"] = JsonSerializer.SerializeToElement(request.Selector), ["maxDepth"] = 32, ["manifestDirectory"] = client.ManifestDirectory
                }, cancellationToken: timeout.Token);
                var actual = JsonSerializer.Deserialize<ToolResult<FindNodesResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(actual.Success, actual.Error?.Message);
                Assert.Equal(Assert.Single(cli.Value.Matches).Node.NodeId, Assert.Single(actual.Value!.Matches).Node.NodeId);
                var duplicate = new Button(); AutomationProperties.SetAutomationId(duplicate, "popup-close"); root.Children.Add(duplicate);
                var ambiguous = await client.QueryNodesAsync(request);
                Assert.True(ambiguous.Success, ambiguous.Error?.Message);
                Assert.Equal(2, ambiguous.Value!.Matches.Count);
                Assert.Equal(2, ambiguous.Value.Matches.Select(match => match.Node.NodeId).Distinct().Count());
            }
            finally { popup.IsOpen = false; File.Delete(path); }
        });
    }

    [Fact]
    public async Task StructuredAndLegacyBoundsAgreeThroughNestedMarginsTransformsAndScrolling()
    {
        await WithWindow(async (runtime, window, root, top, client) =>
        {
            var button = new Button { Name = "Nested", Content = "Nested button", Width = 120, Height = 40,
                RenderTransform = new ScaleTransform(1.15, 1.15), RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative) };
            AutomationProperties.SetAutomationId(button, "nested-button");
            var stack = new StackPanel { Margin = new Thickness(17, 19), Children =
            {
                new Border { Height = 100 },
                new Border { Padding = new Thickness(13, 11), Child = button },
                new Border { Height = 600 }
            }};
            var scroll = new ScrollViewer { Height = 280, Margin = new Thickness(23, 29), Content = stack };
            root.Children.Add(scroll);
            window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            NodeBounds? first = null;
            foreach (var offset in new[] { 0, 70 })
            {
                scroll.Offset = new Vector(0, offset);
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.True((await runtime.ReadinessAsync(top, options: new(waitForFrame: true))).Success);
                var legacy = await client.FindNodesAsync(runtime.SessionId, top, TreeKinds.Visual, automationId: "nested-button", maxDepth: 32);
                var query = await client.QueryNodesAsync(new(runtime.SessionId, top, new(automationId: "nested-button"), maxDepth: 32));
                var oldNode = Assert.Single(legacy.Value!.Matches);
                var newNode = Assert.Single(query.Value!.Matches);
                Assert.Equal(oldNode.Node.NodeId, newNode.Node.NodeId);
                Assert.Equal(oldNode.Node.Bounds, newNode.Node.Bounds);
                var bounds = newNode.Node.Bounds!;
                Assert.NotEqual(button.Bounds.X, bounds.X);
                if (first is null) first = bounds;
                else Assert.Equal(70, first.Y - bounds.Y, 5);
                var topTarget = new RuntimeTargetContext(runtime.SessionId, top, topLevelGeneration: newNode.Target!.TopLevelGeneration);
                var geometry = await client.PickNodeAsync(new(topTarget));
                Assert.True(geometry.Success, geometry.Error?.Message);
                var picked = await client.PickNodeAsync(new(topTarget, bounds.X + bounds.Width / 2,
                    bounds.Y + bounds.Height / 2, "top_level_dip", geometry.Value!.Geometry.Revision));
                Assert.True(picked.Success, picked.Error?.Message);
                Assert.Contains(picked.Value!.HitPath, item => item.Target.NodeId == newNode.Node.NodeId);
                var logicalLegacy = await client.FindNodesAsync(runtime.SessionId, top, TreeKinds.Logical, automationId: "nested-button", maxDepth: 32);
                var logicalQuery = await client.QueryNodesAsync(new(runtime.SessionId, top,
                    new(treeKind: TreeKinds.Logical, automationId: "nested-button"), maxDepth: 32));
                Assert.Equal(Assert.Single(logicalLegacy.Value!.Matches).Node.Bounds, Assert.Single(logicalQuery.Value!.Matches).Node.Bounds);
            }
        });
    }

    [Fact]
    public async Task ExactAutomationIdsKeepCaseDistinctAcrossBridgeCliAndMcp()
    {
        await WithWindow(async (runtime, window, root, top, client) =>
        {
            var lower = new Button { Name = "Lower", Content = "Shared text" };
            var upper = new Button { Name = "Upper", Content = "Shared text" };
            AutomationProperties.SetAutomationId(lower, "Key_a");
            AutomationProperties.SetAutomationId(upper, "Key_A");
            root.Children.Add(lower); root.Children.Add(upper);
            var lowerClicks = 0; var upperClicks = 0;
            lower.Click += (_, _) => lowerClicks++;
            upper.Click += (_, _) => upperClicks++;
            window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            Assert.True((await runtime.ReadinessAsync(top, options: new(waitForFrame: true))).Success);
            foreach (var id in new[] { "Key_a", "Key_A", "KEY_A" })
            {
                var legacy = await client.FindNodesAsync(runtime.SessionId, top, TreeKinds.Visual, automationId: id, maxDepth: 32);
                var query = await client.QueryNodesAsync(new(runtime.SessionId, top, new(automationId: id, actionable: true), maxDepth: 32));
                foreach (var result in new[] { legacy, query })
                {
                    Assert.True(result.Success, result.Error?.Message);
                    if (id == "KEY_A") Assert.Empty(result.Value!.Matches);
                    else
                    {
                        Assert.True(result.Value!.Matches.Count == 1, JsonSerializer.Serialize(new { id, result,
                            window.ClientSize, root = root.Bounds, lower = lower.Bounds, upper = upper.Bounds }));
                        Assert.Equal(id, Assert.Single(result.Value.Matches).Node.AutomationId);
                    }
                }
            }
            var fuzzy = await client.QueryNodesAsync(new(runtime.SessionId, top,
                new(name: "lower", nodeType: "button", text: "SHARED"), maxDepth: 32));
            Assert.Equal("Key_a", Assert.Single(fuzzy.Value!.Matches).Node.AutomationId);
            var excluded = await client.QueryNodesAsync(new(runtime.SessionId, top, new(nodeType: "Button"), maxDepth: 32,
                policy: new(Path.GetTempPath(), excludedControlAutomationIds: ["Key_a"])));
            Assert.Equal("Key_A", Assert.Single(excluded.Value!.Matches).Node.AutomationId);

            var path = Path.Combine(Path.GetTempPath(), "avascope-case-" + Guid.NewGuid().ToString("N") + ".json");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                var environment = TestEnvironment.McpEnvironment();
                if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "Exact IDs", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                    InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3)
                }), cancellationToken: timeout.Token);
                foreach (var id in new[] { "Key_a", "Key_A", "KEY_A" })
                {
                    var request = new RuntimeQueryRequest(runtime.SessionId, top, new(automationId: id), maxDepth: 32);
                    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request), timeout.Token);
                    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "find-nodes", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(argument);
                    using var process = Process.Start(start)!;
                    var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                    var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                    try { await process.WaitForExitAsync(timeout.Token); }
                    finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                    Assert.Equal(0, process.ExitCode);
                    var cli = JsonSerializer.Deserialize<ToolResult<FindNodesResponse>>(await output)!;
                    Assert.True(cli.Success, cli.Error?.Message + await errors);
                    if (id == "KEY_A") Assert.Empty(cli.Value!.Matches);
                    else Assert.Equal(id, Assert.Single(cli.Value!.Matches).Node.AutomationId);
                    foreach (var structured in new[] { false, true })
                    {
                        var arguments = new Dictionary<string, object?>
                        {
                            ["sessionId"] = runtime.SessionId.Value, ["topLevelId"] = top,
                            ["maxDepth"] = 32, ["manifestDirectory"] = client.ManifestDirectory
                        };
                        if (structured) arguments["selector"] = JsonSerializer.SerializeToElement(request.Selector);
                        else arguments["automationId"] = id;
                        var call = await mcp.CallToolAsync("find_nodes", arguments, cancellationToken: timeout.Token);
                        Assert.False(call.IsError == true);
                        var actual = JsonSerializer.Deserialize<ToolResult<FindNodesResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                        Assert.True(actual.Success, actual.Error?.Message);
                        if (id == "KEY_A") Assert.Empty(actual.Value!.Matches);
                        else Assert.Equal(id, Assert.Single(actual.Value!.Matches).Node.AutomationId);
                    }
                    if (id == "KEY_A") continue;
                    var workflow = new SemanticWorkflowRequest(runtime.SessionId, top,
                        [new(SemanticWorkflowActions.Invoke, "invoke", new(automationId: id))], maxDepth: 32);
                    var invoked = await mcp.CallToolAsync("run_workflow", new Dictionary<string, object?>
                    {
                        ["request"] = JsonSerializer.SerializeToElement(workflow), ["manifestDirectory"] = client.ManifestDirectory
                    }, cancellationToken: timeout.Token);
                    Assert.False(invoked.IsError == true);
                    var result = JsonSerializer.Deserialize<ToolResult<SemanticWorkflowResponse>>(JsonSerializer.Serialize(invoked.StructuredContent))!;
                    Assert.Equal("passed", result.Value!.Status);
                    Assert.Equal(1, lowerClicks);
                    Assert.Equal(id == "Key_a" ? 0 : 1, upperClicks);
                }
            }
            finally { File.Delete(path); }
            AutomationProperties.SetAutomationId(upper, "Key_a");
            var ambiguous = await new SemanticWorkflowRunner().RunAsync(client, new(runtime.SessionId, top,
                [new(SemanticWorkflowActions.Invoke, "duplicate", new(automationId: "Key_a"))], maxDepth: 32));
            Assert.Equal("failed", ambiguous.Value!.Status);
            Assert.Contains("candidateCount", JsonSerializer.Serialize(ambiguous.Value));
            Assert.Equal(1, lowerClicks); Assert.Equal(1, upperClicks);
        });
    }

    [Fact]
    public async Task RepeatedFieldsUseContainersParentsDescendantsAndExplicitLocalizedLabels()
    {
        await WithWindow(async (runtime, _, root, top, client) =>
        {
            var shipping = Group("Shipping", new TextBox { Name = "City", Text = "Budapest" });
            var billing = Group("Billing", new TextBox { Name = "City", Text = "Vienna" });
            root.Children.Add(shipping); root.Children.Add(billing);
            var label = new TextBlock { Text = "E-mail cím" };
            var email = new TextBox { Name = "Email", Text = "sample" };
            var second = new TextBox { Name = "SecondEmail" };
            var direct = new Label { Content = "Másodlagos cím", Target = second };
            var unlabeled = new TextBox { Name = "Unlabeled" };
            AutomationProperties.SetLabeledBy(email, label);
            root.Children.Add(label); root.Children.Add(email); root.Children.Add(direct); root.Children.Add(second); root.Children.Add(unlabeled);
            Dispatcher.UIThread.RunJobs();
            var focus = TopLevel.GetTopLevel(root)!.FocusManager!.GetFocusedElement();
            var selector = InGroup("City", "Shipping");
            var found = await client.QueryNodesAsync(new(runtime.SessionId, top, selector, maxDepth: 32));
            Assert.True(found.Success, found.Error?.Message);
            Assert.True(found.Value!.Coverage!.Complete, JsonSerializer.Serialize(found.Value.Coverage));
            var node = Assert.Single(found.Value.Matches);
            Assert.Equal("Budapest", node.Node.Text);
            Assert.Equal("public_visual_ancestor", Assert.Single(node.Relationships).Source);
            var projection = await client.QueryNodesAsync(new(runtime.SessionId, top, selector, ["name", "text", "enabled", "checked"], maxDepth: 32));
            Assert.Empty(projection.Value!.Matches);
            var projected = Assert.Single(projection.Value.Projections);
            Assert.Equal(node.Target!.NodeId, projected.Target.NodeId);
            Assert.Equal(node.Target.SelectionRevision, projected.Target.SelectionRevision);
            Assert.Equal("Budapest", projected.Attributes.Single(value => value.Attribute == "text").Value!.Value.GetString());
            Assert.True(projected.Attributes.Single(value => value.Attribute == "enabled").Value!.Value.GetBoolean());
            Assert.Equal("missing", projected.Attributes.Single(value => value.Attribute == "checked").Status);

            var parent = await client.QueryNodesAsync(new(runtime.SessionId, top,
                new(name: "City", relationships: [new("parent", new(name: "Billing"))]), maxDepth: 32));
            Assert.Equal("Vienna", Assert.Single(parent.Value!.Matches).Node.Text);
            var container = await client.QueryNodesAsync(new(runtime.SessionId, top,
                new(name: "Shipping", relationships: [new("descendant", new(name: "City"))]), maxDepth: 32));
            Assert.Single(container.Value!.Matches);
            var nested = await client.QueryNodesAsync(new(runtime.SessionId, top,
                new(name: "City", relationships: [new("ancestor", new(name: "Shipping", relationships: [new("parent", new(name: "Root"))]))]), maxDepth: 32));
            Assert.Equal(2, Assert.Single(nested.Value!.Matches).Relationships.Count);
            var logical = await client.QueryNodesAsync(new(runtime.SessionId, top,
                new(treeKind: TreeKinds.Logical, name: "City", relationships: [new("ancestor", new(treeKind: TreeKinds.Logical, name: "Shipping"))]), ["text"], maxDepth: 32));
            Assert.Equal("Budapest", Assert.Single(logical.Value!.Projections).Attributes[0].Value!.Value.GetString());
            var labeled = await client.QueryNodesAsync(new(runtime.SessionId, top,
                new(nodeType: "TextBox", relationships: [new("labeled_by", new(text: "E-mail cím"))]), maxDepth: 32));
            Assert.Equal("Email", Assert.Single(labeled.Value!.Matches).Node.Name);
            Assert.Equal("automation_labeled_by", Assert.Single(labeled.Value.Matches[0].Relationships).Source);
            var targeted = await client.QueryNodesAsync(new(runtime.SessionId, top,
                new(nodeType: "TextBox", relationships: [new("labeled_by", new(text: "Másodlagos cím"))]), maxDepth: 32));
            Assert.Equal("SecondEmail", Assert.Single(targeted.Value!.Matches).Node.Name);
            Assert.Equal("label_target", Assert.Single(targeted.Value.Matches[0].Relationships).Source);
            var missing = await client.QueryNodesAsync(new(runtime.SessionId, top,
                new(name: "Unlabeled", relationships: [new("labeled_by", new(text: "E-mail cím"))]), maxDepth: 32));
            Assert.Empty(missing.Value!.Matches);
            Assert.Equal("Unlabeled", Assert.Single(missing.Value.Candidates).Name);
            Assert.Same(focus, TopLevel.GetTopLevel(root)!.FocusManager!.GetFocusedElement());
            Assert.Equal("Budapest", ((TextBox)shipping.Children[0]).Text);
        });
    }

    [Fact]
    public async Task WorkflowsShareRelationshipResolutionAndRejectAmbiguousOrRecycledTargets()
    {
        await WithWindow(async (runtime, _, root, top, client) =>
        {
            var city = new TextBox { Name = "City" };
            var otherCity = new TextBox { Name = "City", Text = "unchanged" };
            root.Children.Add(Group("Shipping", city)); root.Children.Add(Group("Billing", otherCity));
            var peter = Group("Peter", new Button { Name = "Edit", Content = "Edit" });
            var mary = Group("Mary", new Button { Name = "Edit", Content = "Edit" });
            root.Children.Add(peter); root.Children.Add(mary);
            var clicked = 0;
            ((Button)peter.Children[0]).Click += (_, _) => clicked++;
            Dispatcher.UIThread.RunJobs();
            var workflowSelector = InGroup("City", "${group}");
            var workflow = new SemanticWorkflowRequest(runtime.SessionId, top,
            [
                new(SemanticWorkflowActions.ValidateAction, "validate", workflowSelector, text: "Budapest", inputAction: InputActions.KeyText),
                new(SemanticWorkflowActions.TypeText, "type", workflowSelector, text: "Budapest",
                    verify: new(new(SemanticWaitConditionKinds.Text, "Budapest"))),
                new(SemanticWorkflowActions.WaitForState, "wait", workflowSelector, timeoutMs: 2500, pollIntervalMs: 25,
                    waitCondition: new(SemanticWaitConditionKinds.Text, "Budapest"))
            ], maxDepth: 32, variables: new Dictionary<string, string> { ["group"] = "Shipping" });
            var result = await new SemanticWorkflowRunner().RunAsync(client, workflow);
            Assert.True(result.Value!.Status == "passed", JsonSerializer.Serialize(result));
            Assert.Equal("Budapest", city.Text);
            Assert.Equal("unchanged", otherCity.Text);
            var edit = InGroup("Edit", "Peter");
            var target = Assert.Single((await client.QueryNodesAsync(new(runtime.SessionId, top, edit, maxDepth: 32))).Value!.Matches).Target!;
            peter.DataContext = new object();
            var staleInspection = await client.InspectNodeAsync(runtime.SessionId, top, TreeKinds.Visual, target.NodeId!, target: target);
            Assert.False(staleInspection.Success);
            var recycled = await client.InputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: target.NodeId, inputTarget: target);
            Assert.False(recycled.Success);
            Assert.Equal(RuntimeInputErrorCodes.TargetStale, recycled.Error!.Code);
            Assert.Equal("false", recycled.Error.Details!["dispatched"]);
            Assert.Equal(0, clicked);
            var fresh = Assert.Single((await client.QueryNodesAsync(new(runtime.SessionId, top, edit, maxDepth: 32))).Value!.Matches).Target!;
            Assert.NotEqual(target.SelectionRevision, fresh.SelectionRevision);
            var inspected = await client.InspectNodeAsync(runtime.SessionId, top, TreeKinds.Visual, fresh.NodeId!, target: fresh);
            Assert.True(inspected.Success, inspected.Error?.Message);
            Assert.Equal(fresh.SelectionRevision, inspected.Value!.Target.SelectionRevision);
            var current = await client.InputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: fresh.NodeId, inputTarget: fresh);
            Assert.True(current.Success, current.Error?.Message);
            Assert.Equal(1, clicked);
            otherCity.Focus();
            city.GotFocus += (_, _) => city.DataContext = new object();
            var beforeFocus = Assert.Single((await client.QueryNodesAsync(new(runtime.SessionId, top, InGroup("City", "Shipping"), maxDepth: 32))).Value!.Matches).Target!;
            var changedOnFocus = await client.InputAsync(runtime.SessionId, top, InputActions.KeyText, inputText: "wrong-row",
                targetNodeId: beforeFocus.NodeId, inputTarget: beforeFocus);
            Assert.False(changedOnFocus.Success);
            Assert.Equal(BridgeErrorCodes.InvalidInputRequest, changedOnFocus.Error!.Code);
            Assert.Equal("Budapest", city.Text);
            otherCity.Focus();
            beforeFocus = Assert.Single((await client.QueryNodesAsync(new(runtime.SessionId, top, InGroup("City", "Shipping"), maxDepth: 32))).Value!.Matches).Target!;
            var syntheticFocus = await client.InputAsync(runtime.SessionId, top, InputActions.KeyText, inputText: "wrong-row",
                targetNodeId: beforeFocus.NodeId, inputTarget: beforeFocus, execution: new());
            Assert.False(syntheticFocus.Success);
            Assert.Equal("Budapest", city.Text);
            root.Children.Remove(mary);
            root.Children.Add(Group("Peter", new Button { Name = "Edit", Content = "Edit" }));
            var ambiguous = await new SemanticWorkflowRunner().RunAsync(client, new(runtime.SessionId, top,
                [new(SemanticWorkflowActions.Invoke, "edit", edit)], maxDepth: 32));
            Assert.Equal("failed", ambiguous.Value!.Status);
            Assert.Contains("candidateCount", JsonSerializer.Serialize(ambiguous.Value));
            Assert.Equal(1, clicked);
            var missing = await new SemanticWorkflowRunner().RunAsync(client, new(runtime.SessionId, top,
                [new(SemanticWorkflowActions.WaitForState, "gone", InGroup("Missing", "Shipping"), timeoutMs: 200,
                    waitCondition: new(SemanticWaitConditionKinds.Disappears))], maxDepth: 32));
            Assert.Equal("passed", missing.Value!.Status);
            var partial = await new SemanticWorkflowRunner().RunAsync(client, new(runtime.SessionId, top,
                [new(SemanticWorkflowActions.WaitForState, "unknown", InGroup("Missing", "Shipping"), timeoutMs: 100, pollIntervalMs: 25,
                    waitCondition: new(SemanticWaitConditionKinds.Disappears))], maxDepth: 1));
            Assert.Equal("failed", partial.Value!.Status);
        });
    }

    [Fact]
    public async Task QueryCoverageMakesLimitsMissingValuesAndGenerationChangesExplicit()
    {
        await WithWindow(async (runtime, _, root, top, client) =>
        {
            root.Children.Add(Group("First", new TextBox { Name = "City", Text = new string('x', 700) }));
            root.Children.Add(Group("Second", new TextBox { Name = "City" }));
            var changing = new ChangingLabelControl { Name = "Changing" };
            var label = new TextBlock { Text = "Label" };
            changing.Label = label;
            root.Children.Add(changing); root.Children.Add(label);
            Dispatcher.UIThread.RunJobs();
            var nodes = await client.QueryNodesAsync(new(runtime.SessionId, top, new(name: "City"), maxNodes: 1));
            Assert.False(nodes.Value!.Coverage!.Complete);
            Assert.Contains("node_limit", nodes.Value.Coverage.Reasons);
            var limited = await client.QueryNodesAsync(new(runtime.SessionId, top, new(name: "City"), maxResults: 1, maxDepth: 32));
            Assert.Single(limited.Value!.Matches);
            Assert.False(limited.Value.Coverage!.Complete);
            Assert.Contains("result_limit", limited.Value.Coverage.Reasons);
            var projected = await client.QueryNodesAsync(new(runtime.SessionId, top, new(name: "City"), ["text"], maxDepth: 32));
            Assert.Contains(projected.Value!.Projections, p => p.Attributes[0].Status == "truncated" && p.Attributes[0].Value!.Value.GetString()!.Length == 512);
            Assert.Contains(projected.Value.Projections, p => p.Attributes[0].Status == "missing");
            var budgeted = ResponseBudgeter.Apply(projected.Value, maxInlineBytes: 1400, maxItems: 1);
            Assert.False(budgeted.Coverage!.Complete);
            Assert.Contains("response_byte_limit", budgeted.Coverage.Reasons);
            changing.ChangeOnRead = true;
            var changed = await client.QueryNodesAsync(new(runtime.SessionId, top,
                new(name: "Changing", relationships: [new("labeled_by", new(text: "Label"))]), ["name"], maxDepth: 32));
            Assert.False(changed.Value!.Coverage!.Complete);
            Assert.Contains("generation_changed", changed.Value.Coverage.Reasons);
            Assert.Empty(changed.Value.Projections);
        });
    }

    [Fact]
    public async Task RealizedVirtualRowsRequireFreshTargetsAfterScrollingAndRecycling()
    {
        await WithWindow(async (runtime, window, _, top, client) =>
        {
            var invoked = 0;
            var list = new ListBox
            {
                Width = 260, Height = 150, ItemsSource = Enumerable.Range(0, 200).Select(index => new Row("row-" + index)).ToArray(),
                ItemTemplate = new FuncDataTemplate<Row>((_, _) =>
                {
                    var edit = new Button { Name = "Edit", Content = "Edit", Height = 30 };
                    edit.Click += (_, _) => invoked++;
                    var row = new StackPanel { Height = 32, Children = { edit } };
                    row.Bind(AutomationProperties.AutomationIdProperty, new Binding("Id"));
                    return row;
                })
            };
            window.Content = list;
            Dispatcher.UIThread.RunJobs();
            var selector = new SemanticWorkflowSelector(name: "Edit", relationships: [new("ancestor", new(automationId: "row-0"))]);
            var old = Assert.Single((await client.QueryNodesAsync(new(runtime.SessionId, top, selector, maxDepth: 32))).Value!.Matches).Target!;
            Assert.Null(list.ContainerFromIndex(190));
            list.ScrollIntoView(190);
            Dispatcher.UIThread.RunJobs();
            var stale = await client.InputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: old.NodeId, inputTarget: old);
            Assert.False(stale.Success);
            Assert.Equal(0, invoked);
            list.ScrollIntoView(0);
            Dispatcher.UIThread.RunJobs();
            var fresh = Assert.Single((await client.QueryNodesAsync(new(runtime.SessionId, top, selector, maxDepth: 32))).Value!.Matches).Target!;
            Assert.True((await client.InputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: fresh.NodeId, inputTarget: fresh)).Success);
            Assert.Equal(1, invoked);
        });
    }

    [Fact]
    public async Task CliAndMcpProjectIdenticalRedactedValuesWithoutExcludedControls()
    {
        await WithWindow(async (runtime, _, root, top, client) =>
        {
            var secret = "query-private-canary" + new string('q', 600);
            root.Children.Add(Group("Shipping", new TextBox { Name = "City", Text = secret }));
            var hidden = new TextBox { Name = "City", Text = "excluded-city-value" };
            AutomationProperties.SetAutomationId(hidden, "excluded-city");
            root.Children.Add(Group("Billing", hidden));
            Dispatcher.UIThread.RunJobs();
            var path = Path.Combine(Path.GetTempPath(), "avascope-query-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var request = new RuntimeQueryRequest(runtime.SessionId, top, new(name: "City"), ["name", "text", "checked"], maxDepth: 32,
                    policy: new(Path.GetTempPath(), redactedText: [secret], excludedControlAutomationIds: ["excluded-city"]));
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "find-nodes", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(argument);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                try { await process.WaitForExitAsync(timeout.Token); }
                finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                Assert.Equal(0, process.ExitCode);
                var cli = JsonSerializer.Deserialize<ToolResult<FindNodesResponse>>(await output)!;
                Assert.True(cli.Success, cli.Error?.Message + await errors);
                var projected = Assert.Single(cli.Value!.Projections);
                Assert.Equal("redacted", projected.Attributes.Single(value => value.Attribute == "text").Status);
                Assert.Null(projected.Attributes.Single(value => value.Attribute == "text").Value);
                Assert.Equal("missing", projected.Attributes.Single(value => value.Attribute == "checked").Status);
                var environment = TestEnvironment.McpEnvironment();
                if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "Query parity", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                    InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3)
                }), cancellationToken: timeout.Token);
                var call = await mcp.CallToolAsync("find_nodes", new Dictionary<string, object?>
                {
                    ["sessionId"] = runtime.SessionId.Value, ["topLevelId"] = top,
                    ["selector"] = JsonSerializer.SerializeToElement(request.Selector), ["attributes"] = request.Attributes,
                    ["policy"] = JsonSerializer.SerializeToElement(request.Policy), ["maxDepth"] = 32, ["manifestDirectory"] = client.ManifestDirectory
                }, cancellationToken: timeout.Token);
                var actual = JsonSerializer.Deserialize<ToolResult<FindNodesResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(actual.Success, actual.Error?.Message);
                Assert.Equal(JsonSerializer.Serialize(projected.Attributes), JsonSerializer.Serialize(Assert.Single(actual.Value!.Projections).Attributes));
                foreach (var result in new[] { cli, actual })
                {
                    var json = JsonSerializer.Serialize(result);
                    Assert.DoesNotContain("query-private-canary", json);
                    Assert.DoesNotContain("excluded-city", json);
                }
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public void QueryRejectsUnboundedForeignOrMixedTreeExpressions()
    {
        Assert.Throws<ArgumentException>(() => new RuntimeSelectorRelationship("xpath", new(name: "City")));
        Assert.Throws<ArgumentException>(() => new RuntimeQueryRequest(new("s"), "top", new(name: "City"), ["arbitraryProperty"]));
        Assert.Throws<ArgumentException>(() => new RuntimeQueryRequest(new("s"), "top", new(name: "City"), maxNodes: 100000));
        Assert.Throws<ArgumentException>(() => new RuntimeQueryRequest(new("s"), "top", new(treeKind: "logical", name: "City",
            relationships: [new("ancestor", new(name: "Shipping"))])));
        var nested = new SemanticWorkflowSelector(name: "root");
        for (var index = 0; index < 4; index++) nested = new(name: "node", relationships: [new("ancestor", nested)]);
        Assert.Throws<ArgumentException>(() => new SemanticWorkflowSelector(name: "node", relationships: [new("ancestor", nested)]));
    }

    private static SemanticWorkflowSelector InGroup(string name, string group) => new(name: name,
        relationships: [new("ancestor", new(name: group))]);
    private static StackPanel Group(string name, Control child) => new() { Name = name, Children = { child } };
    private sealed record Row(string Id);

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, Window, StackPanel, string, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate();
                var root = new StackPanel { Name = "Root" };
                var window = new Window { Width = 500, Height = 700, Content = root };
                try
                {
                    window.Show();
                    using var registration = runtime.RegisterTopLevel(window);
                    Dispatcher.UIThread.RunJobs();
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    using var preparedFrame = window.CaptureRenderedFrame();
                    Assert.NotNull(preparedFrame);
                    Assert.Equal(new PixelSize(500, 700), preparedFrame.PixelSize);
                    await test(runtime, window, root, top.Id, new(Path.GetDirectoryName(runtime.SessionManifestPath)!));
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    private sealed class ChangingLabelControl : Control
    {
        public Control? Label { get; set; }
        public bool ChangeOnRead { get; set; }
        protected override AutomationPeer OnCreateAutomationPeer() => new ChangingLabelPeer(this);
        private sealed class ChangingLabelPeer(ChangingLabelControl owner) : ControlAutomationPeer(owner)
        {
            protected override AutomationPeer? GetLabeledByCore()
            {
                if (owner.ChangeOnRead) { owner.ChangeOnRead = false; owner.DataContext = new object(); }
                return owner.Label is null ? null : CreatePeerForElement(owner.Label);
            }
        }
    }
}
