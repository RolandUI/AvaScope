using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using AvaScope.ComplexWorkflowApp;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class AgentQaFixtureTests
{
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
