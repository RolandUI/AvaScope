using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Protocol;

namespace AvaScope.ComplexWorkflowApp;

public sealed class App : Application
{
    public const string MainWindowTitle = "AvaScope Complex Workflow";
    public const string DetailsWindowTitle = "AvaScope Complex Details";
    public const string SecretAutomationId = "workflow-sensitive-token";

    private readonly List<IDisposable> _registrations = [];
    private TextBlock? _mainStatus;
    private TextBlock? _detailsStatus;
    private string _commitNote = "unset";

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var detailsWindow = CreateDetailsWindow();
            var (mainWindow, actionTarget) = CreateMainWindow();
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            detailsWindow.Show();

            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions(
                "AvaScope Complex Workflow Sample",
                enableCustomActions: true,
                allowedCustomActions: ["workflow.commit"]));
            _registrations.Add(runtime.RegisterTopLevel(mainWindow));
            _registrations.Add(runtime.RegisterTopLevel(detailsWindow));
            _registrations.Add(runtime.RegisterCustomAction(
                actionTarget,
                new CustomActionRegistration(
                    "workflow.commit",
                    context =>
                    {
                        _commitNote = context.Parameters["note"];
                        _mainStatus!.Text = "Commit accepted";
                        _ = CompleteCommitAsync(_commitNote);
                        return CustomActionOutcome.Succeeded(
                            "Workflow commit accepted.",
                            new Dictionary<string, string> { ["note"] = _commitNote });
                    },
                    "Commits the staged workflow state and publishes it to the details window.",
                    parameters:
                    [
                        new RuntimeCustomActionParameterDescriptor(
                            "note",
                            required: true,
                            description: "Stable note copied to the verified final state.")
                    ],
                    requiredState: new Dictionary<string, string>
                    {
                        ["isVisible"] = "true",
                        ["isEnabled"] = "true"
                    },
                    availability: target => target is Border { IsVisible: true, IsEffectivelyEnabled: true }
                        ? CustomActionAvailability.Available
                        : new CustomActionAvailability(false, "The workflow action target must be visible and enabled."))));

            desktop.Exit += (_, _) => DisposeBridgeRegistrations();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private (Window Window, Border ActionTarget) CreateMainWindow()
    {
        _mainStatus = Text("Ready", "workflow-main-status");
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 20,
            Width = 260,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        AutomationProperties.SetAutomationId(slider, "workflow-standard-slider");

        var dragCard = new WorkflowDragCard
        {
            Width = 150,
            Height = 72,
            Background = Brushes.CornflowerBlue,
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                Text = "Drag workflow card",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        AutomationProperties.SetAutomationId(dragCard, "workflow-drag-source");
        dragCard.DragCompleted += (_, _) => _ = CompleteDragAsync();

        var dropTarget = new Border
        {
            Width = 170,
            Height = 72,
            Margin = new Thickness(28, 0, 0, 0),
            Background = Brushes.MediumSeaGreen,
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                Text = "Drop target",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        AutomationProperties.SetAutomationId(dropTarget, "workflow-drop-target");

        var actionTarget = new Border
        {
            Padding = new Thickness(12),
            Background = Brushes.SlateBlue,
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock { Text = "Application action", Foreground = Brushes.White }
        };
        AutomationProperties.SetAutomationId(actionTarget, "workflow-action-target");

        var finalButton = new Button { Content = "Finalize workflow" };
        AutomationProperties.SetAutomationId(finalButton, "workflow-finalize");
        finalButton.Click += (_, _) => _ = CompleteWorkflowAsync(_commitNote);

        var controls = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = "Repeatable workflow control plane", FontSize = 24 },
                _mainStatus,
                slider,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { dragCard, dropTarget }
                },
                actionTarget
            }
        };

        var optionalUi = IsEnabled("AVASCOPE_COMPLEX_OPTIONAL");
        controls.Children.Add(Text(optionalUi ? "available" : "unavailable", "workflow-optional-state"));
        if (optionalUi)
        {
            var optionalButton = new Button { Content = "Apply optional state" };
            AutomationProperties.SetAutomationId(optionalButton, "workflow-optional-action");
            optionalButton.Click += (_, _) => _mainStatus.Text = "Optional state applied";
            controls.Children.Add(optionalButton);
        }

        controls.Children.Add(finalButton);

        var secret = Environment.GetEnvironmentVariable("AVASCOPE_COMPLEX_SECRET") ?? "local-secret";
        var sensitive = Text($"Sensitive token: {secret}", SecretAutomationId);
        controls.Children.Add(sensitive);
        Console.WriteLine($"Complex workflow secret: {secret}");

        return (new Window
        {
            Title = MainWindowTitle,
            Width = 620,
            Height = 560,
            Content = new ScrollViewer { Content = controls }
        }, actionTarget);
    }

    private Window CreateDetailsWindow()
    {
        _detailsStatus = Text("Waiting for workflow", "workflow-details-status");
        return new Window
        {
            Title = DetailsWindowTitle,
            Width = 420,
            Height = 240,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Workflow details", FontSize = 22 },
                    _detailsStatus
                }
            }
        };
    }

    private async Task CompleteDragAsync()
    {
        _mainStatus!.Text = "Card in transit";
        await Task.Delay(75);
        _mainStatus.Text = "Card delivered";
    }

    private async Task CompleteCommitAsync(string note)
    {
        _detailsStatus!.Text = "Publishing commit";
        await Task.Delay(90);
        _detailsStatus.Text = $"Commit ready: {note}";
    }

    private async Task CompleteWorkflowAsync(string note)
    {
        _detailsStatus!.Text = "Finalizing workflow";
        await Task.Delay(90);
        _detailsStatus.Text = $"Workflow complete: {note}";
    }

    private static TextBlock Text(string text, string automationId)
    {
        var control = new TextBlock { Text = text };
        AutomationProperties.SetAutomationId(control, automationId);
        return control;
    }

    private static bool IsEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private void DisposeBridgeRegistrations()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
        AvaScopeBridge.Deactivate();
    }
}
