using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeFormTests
{
    [Fact]
    public async Task InventoryUsesExplicitLabelsAndReportsUnknownRequiredChoicesAndSensitivity()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var first = new TextBox { Name = "First", Text = "one" };
            var label = new Label { Content = "Duplicate", Target = first };
            var second = new TextBox { Name = "Second", Text = "two", IsReadOnly = true };
            var otherLabel = new TextBlock { Text = "Duplicate" }; AutomationProperties.SetLabeledBy(second, otherLabel);
            var password = new TextBox { Name = "Password", PasswordChar = '*', Text = "private-password" };
            var choices = new ListBox { Name = "Choices", Height = 100, ItemsSource = new[] { "red", "green" }, SelectedIndex = 1 };
            var combo = new ComboBox { Name = "Combo", ItemsSource = new[] { "small", "large" } };
            foreach (var control in new Control[] { label, first, otherLabel, second, password, choices, combo }) root.Children.Add(control);
            Dispatcher.UIThread.RunJobs();
            var inspected = await client.InspectFormAsync(new(runtime.SessionId, top));
            Assert.True(inspected.Success, inspected.Error?.Message);
            var fields = inspected.Value!.Fields;
            Assert.Equal(5, fields.Count); Assert.All(fields, field => Assert.Null(field.Required));
            Assert.Equal("Duplicate", fields.Single(field => field.Name == "First").Label);
            Assert.Equal("label_target", fields.Single(field => field.Name == "First").LabelSource);
            Assert.Equal("automation_labeled_by", fields.Single(field => field.Name == "Second").LabelSource);
            Assert.False(fields.Single(field => field.Name == "Second").Writable);
            var hidden = fields.Single(field => field.Name == "Password");
            Assert.True(hidden.Sensitive); Assert.Null(hidden.Label); Assert.Equal("redacted", hidden.State.Status);
            Assert.DoesNotContain("private-password", JsonSerializer.Serialize(inspected));
            var list = fields.Single(field => field.Name == "Choices");
            Assert.Equal(2, list.ChoiceCount); Assert.True(list.ChoicesComplete);
            Assert.Equal("green", list.Choices[1].Label); Assert.True(list.Choices[1].Selected);
            Assert.NotNull(list.Choices[1].Target); Assert.Equal("present", list.State.Status);
            Assert.Contains("async_pending_unknown", list.Validation.Provenance);
        });
    }

    [Fact]
    public async Task CompletePrevalidationRejectsReadonlyAmbiguousAndOverlongFieldsBeforeInput()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var first = new TextBox { Name = "First", Text = "before" };
            var locked = new TextBox { Name = "Locked", IsReadOnly = true };
            var limited = new TextBox { Name = "Limited", MaxLength = 3 };
            foreach (var field in new[] { first, locked, limited, new TextBox { Name = "Duplicate" }, new TextBox { Name = "Duplicate" } }) root.Children.Add(field);
            root.Children.Add(new CheckBox { Name = "TwoState" });
            var inputCount = 0; first.TextInput += (_, _) => inputCount++;
            Dispatcher.UIThread.RunJobs();
            foreach (var field in new[] { Field("Locked", "changed"), Field("Duplicate", "ambiguous"), Field("Limited", "too-long"), Field("TwoState", null) })
            {
                var result = await client.FillFormAsync(Fill(runtime, top, [Field("First", "after"), field]));
                Assert.Equal("invalid_plan", result.Value!.Status);
                Assert.Equal("not_executed", result.Value.Fields[0].Status);
                Assert.Equal("rejected", result.Value.Fields[1].Status);
                Assert.Equal("before", first.Text); Assert.Equal(0, inputCount);
            }
        });
    }

    [Fact]
    public async Task FillVerifiesTextCheckRangeAndSelectionWithoutImplicitSubmissionAndReplaysExactly()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var text = new TextBox { Name = "Text" }; var check = new CheckBox { Name = "Check" };
            var slider = new Slider { Name = "Slider", Minimum = 0, Maximum = 100 };
            var list = new ListBox { Name = "List", Height = 100, ItemsSource = new[] { "first", "second" } };
            var submit = new Button { Name = "Submit", Content = "Submit", IsDefault = true }; var submissions = 0; submit.Click += (_, _) => submissions++;
            foreach (var control in new Control[] { text, check, slider, list, submit }) root.Children.Add(control);
            Dispatcher.UIThread.RunJobs();
            var inspected = (await client.InspectFormAsync(new(runtime.SessionId, top))).Value!;
            var chosen = inspected.Fields.Single(field => field.Name == "List").Choices[1].Target!;
            var request = Fill(runtime, top, [Field("Text", "árvíztűrő"), Field("Check", true), Field("Slider", 25), Field("List", new[] { chosen })]);
            var result = (await client.FillFormAsync(request)).Value!;
            Assert.Equal("passed", result.Status); Assert.All(result.Fields, field => Assert.True(field.Verified));
            Assert.Equal("árvíztűrő", text.Text); Assert.True(check.IsChecked); Assert.Equal(25, slider.Value); Assert.Equal(1, list.SelectedIndex);
            Assert.False(result.Submitted); Assert.False(result.RolledBack); Assert.Equal(0, submissions);
            Assert.Equal(4, result.ChangedFields.Count);
            text.Text = "changed-later";
            var replayed = (await new LocalBridgeClient(client.ManifestDirectory).FillFormAsync(request)).Value!;
            Assert.True(replayed.Replayed); Assert.Equal(result.ObservedAt, replayed.ObservedAt); Assert.Equal("changed-later", text.Text);
            Assert.Equal("form_request_conflict", (await client.FillFormAsync(new(request.Form, [Field("Text", "other")], request.RequestId))).Error!.Code);
        });
    }

    [Fact]
    public async Task DynamicFieldsAndDependentChoicesAreReportedAndChangedPlansStop()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var check = new CheckBox { Name = "Enable" }; var next = new TextBox { Name = "Next" };
            var choices = new ComboBox { Name = "Choices", ItemsSource = new[] { "old" } };
            check.IsCheckedChanged += (_, _) =>
            {
                next.IsReadOnly = true; choices.ItemsSource = new[] { "new", "other" };
                root.Children.Add(new TextBox { Name = "Appeared" }); Dispatcher.UIThread.RunJobs();
            };
            foreach (var control in new Control[] { check, next, choices }) root.Children.Add(control);
            Dispatcher.UIThread.RunJobs();
            var result = (await client.FillFormAsync(Fill(runtime, top, [Field("Enable", true), Field("Next", "blocked")]))).Value!;
            Assert.Equal("partial", result.Status); Assert.True(check.IsChecked); Assert.True(result.Fields[0].Verified);
            Assert.Equal("plan_changed", result.Fields[1].Status); Assert.Null(next.Text); Assert.Single(result.AppearedFields);
            var changed = result.After!.Fields.Single(field => field.Name == "Choices");
            Assert.Contains(changed.Target.NodeId!, result.ChangedFields); Assert.Equal(2, changed.ChoiceCount);
            Assert.False(result.RolledBack);
        });
    }

    [Fact]
    public async Task SettlingObservesAsyncValidationAndPreservesEarlierEffects()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Text" }; var next = new TextBox { Name = "Next" };
            box.TextChanged += (_, _) => DispatcherTimer.RunOnce(() => DataValidationErrors.SetErrors(box, new[] { "Rejected asynchronously" }), TimeSpan.FromMilliseconds(40));
            root.Children.Add(box); root.Children.Add(next); Dispatcher.UIThread.RunJobs();
            var request = Fill(runtime, top, [Field("Text", "visible-but-invalid"), Field("Next", "not-dispatched")], settleMs: 200);
            var result = (await client.FillFormAsync(request)).Value!;
            Assert.Equal("partial", result.Status); Assert.Equal("visible-but-invalid", box.Text); Assert.Null(next.Text);
            Assert.False(result.Fields[0].Verified); Assert.Equal("not_executed", result.Fields[1].Status);
            Assert.True(result.After!.Fields.Single(field => field.Name == "Text").Validation.HasErrors);
            Assert.True((await client.FillFormAsync(request)).Value!.Replayed);
        });
    }

    [Fact]
    public async Task FinalVerificationCatchesLaterFieldChangingAnEarlierValue()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var first = new TextBox { Name = "First" }; var second = new TextBox { Name = "Second" };
            second.TextChanged += (_, _) => first.Text = "dependency-reset";
            root.Children.Add(first); root.Children.Add(second); Dispatcher.UIThread.RunJobs();
            var result = (await client.FillFormAsync(Fill(runtime, top, [Field("First", "wanted"), Field("Second", "triggers-reset")]))).Value!;
            Assert.Equal("partial", result.Status); Assert.False(result.Fields[0].Verified); Assert.True(result.Fields[1].Verified);
            Assert.Contains(result.Fields[0].Diagnostics, error => error.Code == "form_final_postcondition_changed");
            Assert.Equal("dependency-reset", first.Text);
        });
    }

    [Fact]
    public async Task SensitiveValuesAndPolicyExclusionsRemainPrivateAcrossInspectionFillAndReplay()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var password = new TextBox { Name = "Password", PasswordChar = '*', Text = "old-secret" };
            var box = new TextBox { Name = "Text" }; var excluded = new TextBox { Name = "Excluded", Text = "excluded-value" };
            AutomationProperties.SetAutomationId(excluded, "exclude-me");
            foreach (var control in new[] { password, box, excluded }) root.Children.Add(control);
            Dispatcher.UIThread.RunJobs();
            var denied = (await client.FillFormAsync(Fill(runtime, top, [Field("Password", "new-secret")]))).Value!;
            Assert.Equal("invalid_plan", denied.Status); Assert.Equal("old-secret", password.Text);
            var policy = new RuntimeEvidencePolicy(Path.Combine(Path.GetTempPath(), "avascope-form-policy"),
                redactedText: ["private-text"], excludedControlAutomationIds: ["exclude-me"],
                authorizedSessionIds: [runtime.SessionId.Value], allowedDesiredStates: ["text"]);
            var request = new RuntimeFormFillRequest(new(runtime.SessionId, top, policy: policy),
                [Field("Password", "new-secret"), Field("Text", "private-text")], "private-plan", allowSensitiveInput: true);
            var filled = (await client.FillFormAsync(request)).Value!;
            Assert.Equal("passed", filled.Status); Assert.Equal("new-secret", password.Text); Assert.Equal("private-text", box.Text);
            foreach (var result in new[] { filled, (await client.FillFormAsync(request)).Value! })
            {
                var json = JsonSerializer.Serialize(result);
                foreach (var secret in new[] { "old-secret", "new-secret", "private-text", "excluded-value" }) Assert.DoesNotContain(secret, json);
            }
            var excludedResult = (await client.FillFormAsync(new(request.Form, [Field("Excluded", "denied")], "excluded"))).Value!;
            Assert.Equal("invalid_plan", excludedResult.Status); Assert.Equal("excluded-value", excluded.Text);
            var target = filled.After!.Fields.Single(field => field.Name == "Password").Target;
            var direct = await client.EnsureStateAsync(new(target, "text", JsonSerializer.SerializeToElement("direct-secret"), "password-direct"));
            Assert.True(direct.Value!.Verified); Assert.DoesNotContain("secret", JsonSerializer.Serialize(direct.Value));
            var denyInspection = new RuntimeEvidencePolicy(policy.OwnedEvidenceRoot, allowedActions: [], authorizedSessionIds: [runtime.SessionId.Value]);
            Assert.False((await runtime.InspectFormAsync(new(runtime.SessionId, top, policy: denyInspection))).Success);
            Assert.False((await client.InspectFormAsync(new(runtime.SessionId, top, policy: denyInspection))).Success);
            var readOnlyPolicy = new RuntimeEvidencePolicy(policy.OwnedEvidenceRoot, authorizedSessionIds: [runtime.SessionId.Value]);
            var policyDenied = (await client.FillFormAsync(new(new(runtime.SessionId, top, policy: readOnlyPolicy), [Field("Text", "denied")], "policy-denied"))).Value!;
            Assert.Equal("invalid_plan", policyDenied.Status); Assert.Equal("private-text", box.Text);
        });
    }

    [Fact]
    public async Task FocusRecyclingMakesPartialOutcomeUncertainAndStopsTheNextField()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var first = new TextBox { Name = "First" }; var second = new TextBox { Name = "Second" };
            first.GotFocus += (_, _) => first.DataContext = new object();
            root.Children.Add(first); root.Children.Add(second); Dispatcher.UIThread.RunJobs();
            var request = Fill(runtime, top, [Field("First", "not-safe"), Field("Second", "not-dispatched")]);
            var result = (await client.FillFormAsync(request)).Value!;
            Assert.Equal("uncertain", result.Status); Assert.Equal("uncertain", result.Fields[0].Status);
            Assert.Equal("not_executed", result.Fields[1].Status); Assert.Null(second.Text);
            Assert.True((await client.FillFormAsync(request)).Value!.Replayed);
        });
    }

    [Fact]
    public async Task RoutedRejectionMidFillStopsRemainingFieldsWithoutRollback()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var check = new CheckBox { Name = "Check" }; var rejected = new TextBox { Name = "Rejected" }; var last = new TextBox { Name = "Last" };
            rejected.AddHandler(InputElement.TextInputEvent, (_, args) => args.Handled = true, RoutingStrategies.Tunnel);
            foreach (var control in new Control[] { check, rejected, last }) root.Children.Add(control);
            Dispatcher.UIThread.RunJobs();
            var result = (await client.FillFormAsync(Fill(runtime, top, [Field("Check", true), Field("Rejected", "ignored"), Field("Last", "not-dispatched")]))).Value!;
            Assert.Equal("partial", result.Status); Assert.True(check.IsChecked); Assert.False(result.Fields[1].Verified);
            Assert.Equal("not_executed", result.Fields[2].Status); Assert.Null(last.Text); Assert.False(result.RolledBack);
        });
    }

    [Fact]
    public async Task LostResponseAndFullLedgerNeverCauseRepeatedInput()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var check = new CheckBox { Name = "Check" }; var calls = 0; check.Click += (_, _) => calls++;
            root.Children.Add(check); Dispatcher.UIThread.RunJobs();
            var request = Fill(runtime, top, [Field("Check", true)], settleMs: 0);
            var manifest = client.ListSessionManifests().Single(item => item.SessionId == runtime.SessionId);
            using (var pipe = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await pipe.ConnectAsync(5000);
                await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new BridgeIpcRequest("discard", BridgeIpcMethods.FillForm, formFill: request)) + "\n"));
                await pipe.FlushAsync();
                for (var attempt = 0; calls == 0 && attempt < 100; attempt++) await Task.Delay(10);
                Assert.Equal(1, calls);
            }
            var recovered = await client.FillFormAsync(request);
            Assert.True(recovered.Value!.Replayed); Assert.Equal(1, calls);
            for (var index = 1; index < 64; index++)
                Assert.Equal("passed", (await runtime.FillFormAsync(Fill(runtime, top, [Field("Check", true)], settleMs: 0))).Value!.Status);
            Assert.Equal("form_ledger_full", (await runtime.FillFormAsync(Fill(runtime, top, [Field("Check", false)]))).Error!.Code);
            Assert.True((await runtime.FillFormAsync(request)).Value!.Replayed); Assert.Equal(1, calls);
        });
    }

    [Fact]
    public async Task CliAndMcpExposeEquivalentInventoryFillReplayAndFailureResults()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Text" }; root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var form = new RuntimeFormInspectionRequest(runtime.SessionId, top);
            var request = Fill(runtime, top, [Field("Text", "adapter-value")]);
            var inspected = await Cli<RuntimeFormInspectionResponse>("inspect-form", form, client.ManifestDirectory, 0);
            Assert.Single(inspected.Value!.Fields);
            var filled = await Cli<RuntimeFormFillResponse>("fill-form", request, client.ManifestDirectory, 0);
            Assert.True(filled.Success); Assert.Equal("adapter-value", box.Text);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
            { Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "form-test" }), cancellationToken: timeout.Token);
            var inventory = await mcp.CallToolAsync("inspect_form", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(form), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
            var observed = JsonSerializer.Deserialize<ToolResult<RuntimeFormInspectionResponse>>(JsonSerializer.Serialize(inventory.StructuredContent))!;
            Assert.True(observed.Success); Assert.Equal("adapter-value", Assert.Single(observed.Value!.Fields).State.Value!.Value.GetString());
            var call = await mcp.CallToolAsync("fill_form", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
            var replayed = JsonSerializer.Deserialize<ToolResult<RuntimeFormFillResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
            Assert.True(replayed.Success); Assert.True(replayed.Value!.Replayed);
            box.IsReadOnly = true;
            var rejected = Fill(runtime, top, [Field("Text", "denied")]);
            var failed = await Cli<RuntimeFormFillResponse>("fill-form", rejected, client.ManifestDirectory, 1);
            Assert.False(failed.Success); Assert.Equal("invalid_plan", failed.Value!.Status);
            call = await mcp.CallToolAsync("fill_form", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(rejected), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
            var failure = JsonSerializer.Deserialize<ToolResult<RuntimeFormFillResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
            Assert.False(failure.Success); Assert.True(failure.Value!.Replayed); Assert.Equal("adapter-value", box.Text);
        });
    }

    [Fact]
    public async Task ScopeAndCoverageLimitsRejectIncompletePlansWithoutSideEffects()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var form = new StackPanel { Name = "Form" }; var inside = new TextBox { Name = "Inside" }; form.Children.Add(inside);
            var outside = new TextBox { Name = "Outside" }; root.Children.Add(form); root.Children.Add(outside); Dispatcher.UIThread.RunJobs();
            var scoped = new RuntimeFormInspectionRequest(runtime.SessionId, top, new(name: "Form"));
            Assert.Equal("Inside", Assert.Single((await client.InspectFormAsync(scoped)).Value!.Fields).Name);
            Assert.Equal("invalid_plan", (await client.FillFormAsync(new(scoped, [Field("Outside", "ignored")], "outside"))).Value!.Status);
            var limited = new RuntimeFormInspectionRequest(runtime.SessionId, top, maxFields: 1);
            var result = (await client.FillFormAsync(new(limited, [Field("Inside", "ignored")], "limited"))).Value!;
            Assert.Equal("invalid_plan", result.Status); Assert.Contains("field_limit", result.Before!.Coverage.Reasons); Assert.Null(inside.Text);
            Assert.Throws<ArgumentException>(() => new RuntimeFormInspectionRequest(runtime.SessionId, top, maxFields: 33));
            Assert.Throws<ArgumentException>(() => new RuntimeFormFillRequest(scoped, [Field("Inside", "a"), Field("Inside", "b")], "duplicates"));
            Assert.True(BridgeIpcMethods.RequiresControl(new("id", BridgeIpcMethods.FillForm)));
            Assert.False(BridgeIpcMethods.RequiresControl(new("id", BridgeIpcMethods.InspectForm)));
        });
    }

    private static RuntimeFormFieldInput Field(string name, object? value) => new(name, new(name: name), JsonSerializer.SerializeToElement(value));
    private static RuntimeFormFillRequest Fill(AvaScopeBridgeRuntime runtime, string top, RuntimeFormFieldInput[] fields, int settleMs = 50) =>
        new(new(runtime.SessionId, top), fields, Guid.NewGuid().ToString("N"), settleMs);

    private static async Task<ToolResult<T>> Cli<T>(string command, object request, string manifestDirectory, int exit)
    {
        var path = Path.Combine(Path.GetTempPath(), "avascope-form-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), command, "--request", path, "--manifest-dir", manifestDirectory }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));
            Assert.True(exit == process.ExitCode, await output + await error);
            return JsonSerializer.Deserialize<ToolResult<T>>(await output)!;
        }
        finally { File.Delete(path); }
    }

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, StackPanel, string, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await session.Dispatch(async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var root = new StackPanel(); var window = new Window { Width = 600, Height = 750, Content = root };
                try
                {
                    window.Show(); using var registration = runtime.RegisterTopLevel(window); Dispatcher.UIThread.RunJobs();
                    await test(runtime, root, Assert.Single(await runtime.ListTopLevelsAsync()).Id, new(Path.GetDirectoryName(runtime.SessionManifestPath)!));
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }
}
