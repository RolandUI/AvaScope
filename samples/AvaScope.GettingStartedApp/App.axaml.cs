using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaScope.Bridge;
using AvaScope.GettingStartedApp.Views;
using AvaScope.Protocol;

namespace AvaScope.GettingStartedApp;

public partial class App : Application
{
    private readonly List<IDisposable> _bridgeRegistrations = [];

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow
            {
                DataContext = new SamplePreviewData()
            };
            desktop.MainWindow = window;

            if (IsBridgeEnabled())
            {
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions(
                    "AvaScope Getting Started Sample",
                    enableCustomActions: true,
                    allowedCustomActions: ["confirm", "reset"],
                    allowDestructiveCustomActions: true));
                _bridgeRegistrations.Add(runtime.RegisterTopLevel(window));
                _bridgeRegistrations.Add(runtime.RegisterCustomAction(
                    window.MainContent,
                    new CustomActionRegistration(
                        "confirm",
                        context =>
                        {
                            ((MainView)context.Target).Confirm(context.Parameters.GetValueOrDefault("note"));
                            return CustomActionOutcome.Succeeded("The sample confirmation action completed.");
                        },
                        "Confirms the sample custom control state.",
                        parameters:
                        [
                            new RuntimeCustomActionParameterDescriptor(
                                "note",
                                description: "Optional confirmation note.")
                        ],
                        requiredState: new Dictionary<string, string>
                        {
                            ["isVisible"] = "true",
                            ["isEnabled"] = "true"
                        },
                        availability: target => new CustomActionAvailability(
                            target is MainView { IsVisible: true, IsEffectivelyEnabled: true },
                            target is MainView { IsVisible: true, IsEffectivelyEnabled: true }
                                ? null
                                : "The sample control must be visible and enabled."))));
                _bridgeRegistrations.Add(runtime.RegisterCustomAction(
                    window.MainContent,
                    new CustomActionRegistration(
                        "reset",
                        context =>
                        {
                            ((MainView)context.Target).Reset();
                            return CustomActionOutcome.Succeeded("The sample custom control was reset.");
                        },
                        "Resets the sample custom control state.",
                        RuntimeCustomActionSafetyClassifications.Destructive)));
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static bool IsBridgeEnabled()
    {
        var value = Environment.GetEnvironmentVariable("AVASCOPE_SAMPLE_BRIDGE");
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
