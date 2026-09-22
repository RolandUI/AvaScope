using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
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
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var state = new TextBlock { Name = "Status", Text = "Ready" };
            var editor = new TextBox { Name = "NameField", Text = Environment.GetEnvironmentVariable("AVASCOPE_PROFILE_TEST_SECRET") ?? "Sample" };
            AutomationProperties.SetAutomationId(editor, "native-matrix-sensitive");
            var focusState = new TextBlock { Name = "FocusState", Text = "No focus" };
            editor.GotFocus += (_, _) => focusState.Text = "NameField focused";
            var layoutState = new TextBlock { Name = "LayoutState" };
            var environmentState = new TextBlock { Name = "EnvironmentState", Text = $"theme=Light; font={FontManager.Current.DefaultFontFamily.Name}" };
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
            var modalButton = new Button { Name = "OpenModal", Content = "Open modal" };
            modalButton.Click += async (_, _) =>
            {
                var close = new Button { Name = "CloseModal", Content = "Close modal" };
                var ownerState = new TextBlock { Name = "ModalState" };
                var modal = new Window
                {
                    Title = "Standalone modal", Width = 280, Height = 160,
                    Content = new StackPanel { Children = { ownerState, close } }
                };
                modal.Opened += (_, _) => ownerState.Text = ReferenceEquals(modal.Owner, desktop.MainWindow) ? "Owner verified" : "Owner missing";
                close.Click += (_, _) => modal.Close();
                await modal.ShowDialog(desktop.MainWindow!);
                state.Text = "Modal closed";
            };
            var popupButton = new Button { Name = "OpenPopup", Content = "Open popup" };
            var popup = new Popup
            {
                PlacementTarget = popupButton, IsLightDismissEnabled = false,
                Child = new Border { Padding = new Thickness(12), Background = Brushes.White, Child = new TextBlock { Text = "Native popup" } }
            };
            popup.Opened += (_, _) => state.Text = popup.IsUsingOverlayLayer ? "Popup overlay opened" : "Popup window opened";
            popup.Closed += (_, _) => state.Text = "Popup closed";
            popupButton.Click += (_, _) => popup.IsOpen = true;
            var closePopup = new Button { Name = "ClosePopup", Content = "Close popup" };
            closePopup.Click += (_, _) => popup.IsOpen = false;
            var resize = new Button { Name = "Resize", Content = "Resize" };
            resize.Click += (_, _) => { desktop.MainWindow!.Width = 580; desktop.MainWindow.Height = 520; };
            desktop.MainWindow = new Window
            {
                Title = "Standalone inspection sample", Width = 500, Height = 460,
                Content = new StackPanel
                {
                    Margin = new Thickness(16), Spacing = 8,
                    Children = { state, editor, focusState, layoutState, environmentState, open, modalButton, popupButton, closePopup, resize, quit, popup }
                }
            };
            desktop.MainWindow.SizeChanged += (_, _) => layoutState.Text = FormattableString.Invariant(
                $"{desktop.MainWindow.ClientSize.Width:0}x{desktop.MainWindow.ClientSize.Height:0}");

            // The host owns this compile-time authorization. Merely supplying files or an
            // environment variable cannot enable inspection in the normal build.
#if ENABLE_UI_INSPECTION
            if (desktop.Args?.Contains("--load-only", StringComparer.Ordinal) == true)
            {
                var path = Path.Combine(Environment.GetEnvironmentVariable("UI_INSPECTION_PROVIDER_PATH")!, "AvaScope.Bridge.dll");
                var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                _ = assembly.GetType("AvaScope.Bridge.Bootstrap", throwOnError: true);
                Console.Error.WriteLine("AVASCOPE_PROVIDER_LOADED_ONLY");
            }
            else
            {
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
                    if (desktop.Args?.Contains("--declare-readiness", StringComparer.Ordinal) == true)
                    {
                        // This optional host-owned hook still has no AvaScope type reference.
                        var bootstrap = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == "AvaScope.Bridge")
                            .GetType("AvaScope.Bridge.Bootstrap", throwOnError: true)!;
                        var declare = bootstrap.GetMethod("SetReadiness", [typeof(string), typeof(string)])!;
                        declare.Invoke(null, ["starting", "Loading fixture data"]);
                        DispatcherTimer.RunOnce(() =>
                        {
                            editor.Width = 300;
                            declare.Invoke(null, ["ready", "Fixture data loaded"]);
                        }, TimeSpan.FromMilliseconds(250));
                    }
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
