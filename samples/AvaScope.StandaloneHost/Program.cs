using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace StandaloneHost;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = AppBuilder.Configure<SampleApplication>();
        if (args.Contains("--headless", StringComparer.Ordinal))
        {
            builder.UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
        }
        else
        {
            builder.UsePlatformDetect();
        }

        builder.StartWithClassicDesktopLifetime(args);
    }
}

internal sealed class SampleApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var state = new TextBlock { Name = "Status", Text = "Ready" };
            var editor = new TextBox { Name = "NameField", Text = "Sample" };
            var open = new Button { Name = "OpenWindow", Content = "Open child window" };
            AutomationProperties.SetAutomationId(open, "open-window");
            open.Click += (_, _) =>
            {
                var close = new Button { Name = "CloseChild", Content = "Close child" };
                var child = new Window { Title = "Standalone child", Width = 280, Height = 160, Content = close };
                close.Click += (_, _) => child.Close();
                child.Closed += (_, _) => state.Text = "Child closed";
                child.Show();
                state.Text = "Child opened";
            };
            var quit = new Button { Name = "Quit", Content = "Quit" };
            quit.Click += (_, _) => desktop.Shutdown();
            desktop.MainWindow = new Window
            {
                Title = "Standalone inspection sample", Width = 400, Height = 260,
                Content = new StackPanel { Margin = new Thickness(16), Spacing = 8, Children = { state, editor, open, quit } }
            };

            // The host owns this compile-time authorization. Merely supplying files or an
            // environment variable cannot enable inspection in the normal build.
#if ENABLE_UI_INSPECTION
            var result = OptionalDiagnostics.OptionalProviderLoader.TryStartFromEnvironment(
                expectedVersion: Environment.GetEnvironmentVariable("UI_INSPECTION_PROVIDER_VERSION"),
                expectedManifestSha256: Environment.GetEnvironmentVariable("UI_INSPECTION_PROVIDER_SHA256"));
            Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
            if (result.Activated)
            {
                // Exercise idempotent reflection activation without any host registration.
                var repeated = OptionalDiagnostics.OptionalProviderLoader.TryStartFromEnvironment();
                if (!repeated.Success || repeated.SessionId != result.SessionId)
                {
                    throw new InvalidOperationException("Repeated provider activation changed the session.");
                }
            }
#endif

            var exitArgument = desktop.Args?.FirstOrDefault(argument => argument.StartsWith("--exit-after-ms=", StringComparison.Ordinal));
            if (exitArgument is not null && int.TryParse(exitArgument[16..], out var milliseconds) && milliseconds is > 0 and <= 120000)
            {
                DispatcherTimer.RunOnce(() => desktop.Shutdown(), TimeSpan.FromMilliseconds(milliseconds));
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
