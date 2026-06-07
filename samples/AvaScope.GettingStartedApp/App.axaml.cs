using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaScope.Bridge;
using AvaScope.GettingStartedApp.Views;

namespace AvaScope.GettingStartedApp;

public partial class App : Application
{
    private IDisposable? _bridgeRegistration;

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
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("AvaScope Getting Started Sample"));
                _bridgeRegistration = runtime.RegisterTopLevel(window);
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
