using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Collections;
using Avalonia;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.ComponentModel;
using Avalonia.Headless;
using Avalonia.Interactivity;
using AvaScope.ComplexWorkflowApp;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class AgentQaFixtureTests
{
    [Theory]
    [InlineData("stop")]
    [InlineData("reset")]
    [InlineData("cleanup")]
    [InlineData("close")]
    public async Task TimelineAnimationKeepsReferencesAndCancelsWithoutLateStateWrites(string cleanup)
    {
        var directory = Path.Combine(Path.GetTempPath(), "avascope-qa-animation-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable("AVASCOPE_QA_OUTPUT");
        Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", directory);
        try
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                var window = new QaWindow(); window.Show();
                try
                {
                    window.FindControl<TabControl>("Pages")!.SelectedIndex = 1;
                    var names = new[] { "AnimationStyleTarget", "AnimationStyleReference", "AnimationLocalTarget", "AnimationLocalReference" };
                    var controls = names.Select(name => window.FindControl<Border>(name)!).ToArray();
                    using (var frame = window.CaptureRenderedFrame()) Assert.NotNull(frame);
                    foreach (var control in controls)
                    {
                        Assert.Equal(Colors.Red, Assert.IsAssignableFrom<ISolidColorBrush>(control.Background).Color);
                        Assert.Equal(control.Name!.Contains("Style") ? BindingPriority.StyleTrigger : BindingPriority.LocalValue,
                            control.GetDiagnostic(Border.BackgroundProperty).Priority);
                    }
                    var animation = window.RunAnimationAsync();
                    await window.RunAnimationAsync(); // A second start must not stack animations.
                    using (var frame = window.CaptureRenderedFrame()) Assert.NotNull(frame);
                    foreach (var control in controls)
                    {
                        Assert.True(control.IsAnimating(Border.BackgroundProperty));
                        Assert.Equal(BindingPriority.Animation, control.GetDiagnostic(Border.BackgroundProperty).Priority);
                        Assert.NotEqual(Colors.Red, Assert.IsAssignableFrom<ISolidColorBrush>(control.Background).Color);
                    }
                    window.FindControl<Button>("SampleAnimationButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    using (var running = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json"))))
                    {
                        var state = running.RootElement.GetProperty("animation");
                        Assert.True(state.GetProperty("running").GetBoolean());
                        Assert.Equal(1, state.GetProperty("starts").GetInt32());
                        Assert.Equal(JsonValueKind.Null, state.GetProperty("error").ValueKind);
                        Assert.All(state.GetProperty("controls").EnumerateArray(), control => Assert.True(control.GetProperty("isAnimating").GetBoolean()));
                    }
                    if (cleanup == "reset") window.ResetState();
                    else if (cleanup == "cleanup") window.CleanupState();
                    else if (cleanup == "close") window.Close();
                    else window.FindControl<Button>("StopAnimationButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var stoppedJournal = File.ReadAllText(Path.Combine(directory, "qa-state.json"));
                    await animation;
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(stoppedJournal, File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                    foreach (var control in controls)
                    {
                        Assert.False(control.IsAnimating(Border.BackgroundProperty));
                        if (cleanup == "close" && control.Name!.Contains("Style"))
                        {
                            // Closing detaches the window's styles, so neither the animated
                            // target nor its reference should retain a styled brush.
                            Assert.Null(control.Background);
                            continue;
                        }
                        Assert.Equal(Colors.Red, Assert.IsAssignableFrom<ISolidColorBrush>(control.Background).Color);
                        Assert.Equal(control.Name!.Contains("Style") ? BindingPriority.StyleTrigger : BindingPriority.LocalValue,
                            control.GetDiagnostic(Border.BackgroundProperty).Priority);
                    }
                    using var stopped = JsonDocument.Parse(stoppedJournal);
                    var stoppedState = stopped.RootElement.GetProperty("animation");
                    Assert.False(stoppedState.GetProperty("running").GetBoolean());
                    Assert.Equal(cleanup == "reset" ? 0 : 1, stoppedState.GetProperty("stops").GetInt32());
                    Assert.Equal(cleanup == "reset" ? "Idle" : "Stopped", stoppedState.GetProperty("status").GetString());
                }
                finally { window.Close(); }
            }, CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", previous);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task KeyedListCanRevealAndSelectOffscreenRowsWithoutChangingTableOrderAndResetRestoresItsSeed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avascope-qa-items-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable("AVASCOPE_QA_OUTPUT");
        Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", directory);
        try
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("QA keyed list"));
                var window = new QaWindow(); window.Show();
                try
                {
                    using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    var rows = window.FindControl<ListBox>("Rows")!;
                    var table = window.FindControl<DataGrid>("RecordsTable")!;
                    for (var cycle = 0; cycle < 2; cycle++)
                    {
                        window.FindControl<TabControl>("Pages")!.SelectedIndex = 2;
                        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                        var seed = rows.ItemsSource!.Cast<QaRecord>().ToArray();
                        Assert.Equal(200, seed.Length);
                        Assert.Equal("QA-001", seed[0].Id);
                        Assert.Null(rows.ContainerFromIndex(174));
                        var found = await runtime.FindNodesAsync(top.Id, TreeKinds.Visual, automationId: "qa-rows", maxDepth: 32);
                        Assert.True(found.Success, found.Error?.Message);
                        var target = Assert.Single(found.Value!.Matches).Node.Target!;
                        var lookup = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "QA-175"));
                        Assert.True(lookup.Success, lookup.Error?.Message);
                        Assert.False(lookup.Value!.Realized);
                        Assert.Equal(174, lookup.Value.Index);
                        Assert.Null(rows.SelectedItem);
                        var selected = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "QA-175", "select"));
                        Assert.True(selected.Success, JsonSerializer.Serialize(new
                        {
                            cycle, response = selected,
                            listSelection = (rows.SelectedItem as QaRecord)?.Id,
                            tableSelection = (table.SelectedItem as QaRecord)?.Id,
                            containerBounds = rows.ContainerFromIndex(174)?.Bounds.ToString(),
                            listBounds = rows.Bounds.ToString(),
                            clientSize = window.ClientSize.ToString(),
                            journal = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, "qa-state.json")))
                        }));
                        Assert.True(selected.Value!.Rendered);
                        Assert.Same(seed[174], rows.SelectedItem);
                        Assert.Null(table.SelectedItem);
                        Assert.Contains(rows.ContainerFromIndex(174)!.GetVisualDescendants().OfType<TextBlock>(),
                            text => text.Text == "Record 175 — seeded QA data");
                        using (var journal = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json"))))
                        {
                            Assert.Equal(174, journal.RootElement.GetProperty("selectedRow").GetInt32());
                            Assert.Equal("QA-175", journal.RootElement.GetProperty("selectedRowKey").GetString());
                        }
                        var reveal = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "QA-001", "reveal"));
                        Assert.True(reveal.Success, JsonSerializer.Serialize(reveal));
                        Assert.True(reveal.Value!.Rendered);
                        Assert.Same(seed[174], rows.SelectedItem);
                        table.CollectionView.SortDescriptions.Add(DataGridSortDescription.FromPath("Id", ListSortDirection.Descending));
                        Assert.Equal("QA-200", table.ItemsSource.Cast<QaRecord>().First().Id);
                        Assert.Equal("QA-001", rows.ItemsSource!.Cast<QaRecord>().First().Id);
                        var beforeRefusal = File.ReadAllText(Path.Combine(directory, "qa-state.json"));
                        var missing = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "QA-999", "select"));
                        Assert.Equal("virtual_item_not_found", missing.Error!.Code);
                        var limited = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "QA-175", "select", maxItems: 100));
                        Assert.Equal("virtual_item_search_limit", limited.Error!.Code);
                        var staleTarget = new RuntimeTargetContext(target.SessionId, target.TopLevelId, target.TreeKind,
                            target.NodeId, nodeGeneration: "stale");
                        var stale = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(staleTarget, "Id", "QA-175", "select"));
                        Assert.Equal("virtual_item_stale_collection", stale.Error!.Code);
                        Assert.Same(seed[174], rows.SelectedItem);
                        Assert.Equal(beforeRefusal, File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                        window.ResetState();
                        Assert.Null(rows.SelectedItem);
                        Assert.Equal(0, window.FindControl<TabControl>("Pages")!.SelectedIndex);
                        Assert.NotSame(seed[174], rows.ItemsSource!.Cast<QaRecord>().ElementAt(174));
                        Assert.Empty(table.CollectionView.SortDescriptions);
                        using var reset = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                        Assert.Equal(-1, reset.RootElement.GetProperty("selectedRow").GetInt32());
                        Assert.Equal(JsonValueKind.Null, reset.RootElement.GetProperty("selectedRowKey").ValueKind);
                    }
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
    [InlineData(1028, 749)]
    [InlineData(680, 620)]
    public async Task ContentStaysWithinObservedClientAfterReset(int width, int height)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        await BridgeHeadlessSmokeTests.DispatchAsync(session, () =>
        {
            var window = new QaWindow { Width = width, Height = height }; window.Show();
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            try
            {
                for (var cycle = 0; cycle < 2; cycle++)
                {
                    window.ResetState();
                    window.Width = width; window.Height = height;
                    window.FindControl<TabControl>("Pages")!.SelectedIndex = 5;
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    // Reproduce the native failure boundary: content is arranged at the
                    // requested 1120x800 while the actual client remains clamped.
                    var content = Assert.IsAssignableFrom<Control>(window.Content);
                    content.Measure(new Size(1120, 800));
                    content.Arrange(new Rect(0, 0, 1120, 800));
                    var reset = window.FindControl<Button>("ResetButton")!;
                    var origin = reset.TranslatePoint(default, window)!.Value;
                    Assert.True(origin.X + reset.Bounds.Width <= window.ClientSize.Width,
                        $"Reset exceeds client at {width}, cycle {cycle}: {origin} + {reset.Bounds.Size}; client {window.ClientSize}.");
                    var hit = window.GetVisualAt(origin + new Vector(reset.Bounds.Width / 2, reset.Bounds.Height / 2));
                    Assert.True(hit == reset || hit?.GetVisualAncestors().Contains(reset) == true);
                    Assert.NotEmpty(window.FindControl<DataGrid>("RecordsTable")!.GetVisualDescendants().OfType<DataGridRow>());
                }
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task InvalidFormCannotSaveAndResetRemovesSavedStateAndValidationWithoutJournalingPasswords()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avascope-qa-form-" + Guid.NewGuid().ToString("N"));
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
                    for (var cycle = 0; cycle < 2; cycle++)
                    {
                        window.Width = cycle == 0 ? 680 : 1120;
                        window.FindControl<TabControl>("Pages")!.SelectedIndex = 4;
                        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                        var role = window.FindControl<ComboBox>("FormRole")!;
                        var account = window.FindControl<TextBox>("FormAccount")!;
                        Assert.True(role.TranslatePoint(new Point(role.Bounds.Width, 0), window)!.Value.X
                            < account.TranslatePoint(default, window)!.Value.X, "Form columns must not overlap.");
                        var name = window.FindControl<TextBox>("FormName")!;
                        var email = window.FindControl<TextBox>("FormEmail")!;
                        var submit = window.FindControl<Button>("SubmitFormButton")!;
                        window.FindControl<TextBox>("FormPassword")!.Text = "never-write-this-synthetic-password";
                        name.Text = " "; email.Text = "invalid";
                        submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Assert.True(DataValidationErrors.GetHasErrors(name));
                        Assert.True(DataValidationErrors.GetHasErrors(email));
                        using (var rejected = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json"))))
                            Assert.Equal(0, rejected.RootElement.GetProperty("form").GetProperty("saves").GetInt32());
                        name.Text = "Grace Hopper"; email.Text = "grace@example.test";
                        window.FindControl<ComboBox>("FormRole")!.SelectedIndex = 2;
                        window.FindControl<Slider>("FormPriority")!.Value = 80;
                        submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        var savedText = File.ReadAllText(Path.Combine(directory, "qa-state.json"));
                        Assert.DoesNotContain("never-write-this-synthetic-password", savedText);
                        using (var saved = JsonDocument.Parse(savedText))
                        {
                            var form = saved.RootElement.GetProperty("form");
                            Assert.Equal(1, form.GetProperty("saves").GetInt32());
                            Assert.Equal("Reviewer", form.GetProperty("savedProfile").GetProperty("role").GetString());
                            Assert.Equal(80, form.GetProperty("savedProfile").GetProperty("priority").GetDouble());
                        }
                        // Rejection after a successful save preserves the last committed profile.
                        email.Text = "invalid-again";
                        submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        using (var rejected = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json"))))
                            Assert.Equal("grace@example.test", rejected.RootElement.GetProperty("form").GetProperty("savedProfile").GetProperty("email").GetString());
                        window.ResetState();
                        Assert.False(DataValidationErrors.GetHasErrors(email));
                        Assert.Equal("Ada Lovelace", name.Text);
                        using var reset = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                        Assert.Equal(0, reset.RootElement.GetProperty("form").GetProperty("saves").GetInt32());
                        Assert.Equal(JsonValueKind.Null, reset.RootElement.GetProperty("form").GetProperty("savedProfile").ValueKind);
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
    public async Task TableResetRestoresOrderAndSelectionAndDetachedRowsCannotChangeTheJournal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avascope-qa-table-" + Guid.NewGuid().ToString("N"));
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
                    var table = window.FindControl<DataGrid>("RecordsTable")!;
                    for (var cycle = 0; cycle < 2; cycle++)
                    {
                        var row = table.ItemsSource.Cast<QaRecord>().Single(item => item.Id == "QA-175");
                        window.FindControl<TabControl>("Pages")!.SelectedIndex = 5;
                        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                        Assert.NotNull(table.Template);
                        Assert.NotEmpty(table.GetVisualDescendants().OfType<DataGridRow>());
                        row.Status = "reviewed"; row.Score = 99;
                        table.CollectionView.SortDescriptions.Add(DataGridSortDescription.FromPath("Id", ListSortDirection.Descending));
                        table.SelectedItem = row;
                        using (var changed = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json"))))
                        {
                            var state = changed.RootElement.GetProperty("table");
                            Assert.Equal(2, state.GetProperty("edits").GetInt32());
                            Assert.Equal("QA-175", state.GetProperty("selectedKey").GetString());
                            Assert.Equal("QA-200", state.GetProperty("viewOrder")[0].GetString());
                            Assert.Equal("reviewed", state.GetProperty("rows")[174].GetProperty("status").GetString());
                        }
                        window.ResetState();
                        var before = File.ReadAllText(Path.Combine(directory, "qa-state.json"));
                        row.Status = "detached";
                        Assert.Equal(before, File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                        Assert.Empty(table.CollectionView.SortDescriptions);
                        Assert.Null(table.SelectedItem);
                        using var reset = JsonDocument.Parse(before);
                        Assert.Equal(0, reset.RootElement.GetProperty("table").GetProperty("edits").GetInt32());
                        Assert.Equal("QA-001", reset.RootElement.GetProperty("table").GetProperty("viewOrder")[0].GetString());
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
    public async Task PointerPadDistinguishesClicksFromDragsAndResetClearsInputState()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avascope-qa-pointer-" + Guid.NewGuid().ToString("N"));
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
                    window.FindControl<TabControl>("Pages")!.SelectedIndex = 6;
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    var pad = window.FindControl<Border>("PointerPad")!;
                    var point = pad.TranslatePoint(new Point(30, 30), window)!.Value;
                    window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
                    using (var click = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json"))))
                    {
                        Assert.Equal(1, click.RootElement.GetProperty("input").GetProperty("pointerReleases").GetInt32());
                        Assert.Equal(0, click.RootElement.GetProperty("input").GetProperty("drags").GetInt32());
                    }
                    var end = point + new Point(80, 30);
                    window.MouseDown(point, MouseButton.Left); window.MouseMove(end); window.MouseUp(end, MouseButton.Left);
                    using (var dragged = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json"))))
                    {
                        Assert.Equal(1, dragged.RootElement.GetProperty("input").GetProperty("drags").GetInt32());
                        Assert.Equal(80, dragged.RootElement.GetProperty("input").GetProperty("dragX").GetDouble());
                        Assert.Equal(30, dragged.RootElement.GetProperty("input").GetProperty("dragY").GetDouble());
                    }
                    window.ResetState();
                    using var reset = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                    Assert.Equal(0, reset.RootElement.GetProperty("input").GetProperty("drags").GetInt32());
                    Assert.Equal(0, reset.RootElement.GetProperty("input").GetProperty("pointerPresses").GetInt32());
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
    public async Task RequestedSceneSizeSurvivesNativeClampingAndTogglesBackToCompact()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avascope-qa-size-" + Guid.NewGuid().ToString("N"));
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
                    var button = window.FindControl<Button>("SizeButton")!;
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    // Model the platform coercing the public size properties on a smaller screen.
                    window.Width = 1300; window.Height = 900;
                    window.FindControl<Button>("ThemeButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    using (var full = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json"))))
                    {
                        Assert.Equal(1920, full.RootElement.GetProperty("requestedWidth").GetDouble());
                        Assert.Equal(1080, full.RootElement.GetProperty("requestedHeight").GetDouble());
                    }
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(1120, window.Width); Assert.Equal(800, window.Height);
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    window.ResetState();
                    using var reset = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                    Assert.Equal(1120, reset.RootElement.GetProperty("requestedWidth").GetDouble());
                    Assert.Equal(800, reset.RootElement.GetProperty("requestedHeight").GetDouble());
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
    public async Task ResetCancelsDelayedWorkClosesChildrenAndRestoresIndependentState()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avascope-qa-fixture-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable("AVASCOPE_QA_OUTPUT");
        Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", directory);
        try
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                var window = new QaWindow();
                window.Show();
                try
                {
                    window.FindControl<ToggleButton>("Notifications")!.IsChecked = true;
                    window.FindControl<TextBox>("DisplayNameEditor")!.Text = "Changed profile";
                    window.FindControl<Button>("ChildButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    window.FindControl<Button>("DelayedButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    using (var changed = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json"))))
                    {
                        Assert.Equal("Changed profile", changed.RootElement.GetProperty("displayName").GetString());
                        Assert.Equal(1, changed.RootElement.GetProperty("toggleCount").GetInt32());
                        Assert.Equal(1, changed.RootElement.GetProperty("childWindows").GetInt32());
                        Assert.Equal("Loading", changed.RootElement.GetProperty("loadStatus").GetString());
                    }
                    window.ResetState();
                    // Let the cancelled fixture operation reach its completion boundary.
                    await Task.Delay(550);
                    using var reset = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                    Assert.Equal("Ada", reset.RootElement.GetProperty("displayName").GetString());
                    Assert.False(reset.RootElement.GetProperty("notifications").GetBoolean());
                    Assert.Equal(0, reset.RootElement.GetProperty("toggleCount").GetInt32());
                    Assert.Equal(0, reset.RootElement.GetProperty("textChanges").GetInt32());
                    Assert.Equal(0, reset.RootElement.GetProperty("childWindows").GetInt32());
                    Assert.Equal("Ready", reset.RootElement.GetProperty("loadStatus").GetString());
                    Assert.Equal(2, reset.RootElement.GetProperty("resetGeneration").GetInt32());
                }
                finally { window.Close(); }
            }, CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", previous);
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ReplacementAndCaseDistinctControlsKeepTheirOwnObservableState()
    {
        var directory = Path.Combine(Path.GetTempPath(), "avascope-qa-identity-" + Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable("AVASCOPE_QA_OUTPUT");
        Environment.SetEnvironmentVariable("AVASCOPE_QA_OUTPUT", directory);
        try
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
            await BridgeHeadlessSmokeTests.DispatchAsync(session, () =>
            {
                var window = new QaWindow();
                window.Show();
                try
                {
                    var oldEditor = window.FindControl<TextBox>("DisplayNameEditor")!;
                    window.FindControl<Button>("ReplaceEditorButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    oldEditor.Text = "Detached editor must not change current state";
                    var editor = Assert.IsType<TextBox>(window.FindControl<StackPanel>("EditorHost")!.Children.Single());
                    editor.Text = "Current editor";
                    window.FindControl<Button>("LowercaseButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    window.FindControl<Button>("UppercaseButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    window.FindControl<Button>("UppercaseButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "qa-state.json")));
                    Assert.Equal("Current editor", state.RootElement.GetProperty("displayName").GetString());
                    Assert.Equal(1, state.RootElement.GetProperty("editorGeneration").GetInt32());
                    Assert.Equal(1, state.RootElement.GetProperty("textChanges").GetInt32());
                    Assert.Equal(1, state.RootElement.GetProperty("lowercaseCount").GetInt32());
                    Assert.Equal(2, state.RootElement.GetProperty("uppercaseCount").GetInt32());
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
}
