using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Collections;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using System.ComponentModel;
using Avalonia.Headless;
using Avalonia.Interactivity;
using AvaScope.ComplexWorkflowApp;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class AgentQaFixtureTests
{
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
