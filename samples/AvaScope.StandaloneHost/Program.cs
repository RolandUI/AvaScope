using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
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
#if WAYLAND_FIXTURE
        else if (args.Contains("--wayland", StringComparer.Ordinal))
        {
            // Desktop defaults configure Skia/text shaping; UseWayland explicitly replaces
            // the windowing backend and has no automatic X11 fallback.
            builder.UsePlatformDetect().UseWayland().With(new WaylandPlatformOptions { EnableReconnects = false, UseDmabufSwapchain = false });
        }
#endif
        else
        {
            builder.UsePlatformDetect();
            if (args.Contains("--gtk-picker", StringComparer.Ordinal))
                builder.With(new X11PlatformOptions { UseDBusFilePicker = false });
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
            if (desktop.Args?.Contains("--input-fixture", StringComparer.Ordinal) == true)
                ConfigureInputFixture(desktop.MainWindow);
            if (desktop.Args?.Contains("--screen-fixture", StringComparer.Ordinal) == true)
                ConfigureScreenFixture(desktop.MainWindow);
            if (desktop.Args?.Contains("--accessibility-fixture", StringComparer.Ordinal) == true)
                ConfigureAccessibilityFixture(desktop.MainWindow);

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
                    if (desktop.Args?.Contains("--authorize-screen-capture", StringComparer.Ordinal) == true)
                    {
                        // Explicit test-host authorization, not an agent-controlled environment switch in the bridge.
                        AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == "AvaScope.Bridge")
                            .GetType("AvaScope.Bridge.Bootstrap", throwOnError: true)!.GetMethod("SetNativeScreenCaptureScope")!
                            .Invoke(null, ["declared_test_desktop"]);
                    }
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

    private static void ConfigureAccessibilityFixture(Window window)
    {
        window.Width = 420; window.Height = 360;
        var good = new Button { Name = "AuditGood", Content = "Save private-document", Width = 220, Height = 40 };
        var unnamed = new Button { Name = "AuditUnnamed", Width = 220, Height = 40 };
        var wrongRole = new WrongRoleAuditButton { Name = "AuditWrongRole", Content = "Run", Width = 220, Height = 40 };
        var absent = new AbsentAuditButton { Name = "AuditAbsent", Width = 220, Height = 40 };
        foreach (var control in new Control[] { good, unnamed, wrongRole, absent }) AutomationProperties.SetAutomationId(control, control.Name);
        AutomationProperties.SetName(good, "Save private-document");
        window.Content = new StackPanel { Spacing = 8, Children = { good, unnamed, wrongRole, absent,
            new Border { Name = "AuditDecoration", Background = Brushes.Blue, Height = 12 }, new TextBlock { Text = "Grouped decorative label" } } };
    }

    private sealed class WrongRoleAuditButton : Button
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);
        private sealed class Peer(Button owner) : ButtonAutomationPeer(owner)
        { protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image; }
    }
    private sealed class AbsentAuditButton : Button
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);
        private sealed class Peer(Button owner) : ButtonAutomationPeer(owner)
        {
            protected override bool IsControlElementCore() => false;
            protected override bool IsContentElementCore() => false;
        }
    }

    private static void ConfigureScreenFixture(Window window)
    {
        window.Width = 400; window.Height = 300; window.Topmost = true;
        Window? occluder = null;
        var open = new Button { Name = "ScreenOcclude", Content = "Occlude" };
        var close = new Button { Name = "ScreenUncover", Content = "Uncover" };
        var move = new Button { Name = "ScreenOffscreen", Content = "Offscreen" };
        var popupButton = new Button { Name = "ScreenPopup", Content = "Popup" };
        var popup = new Popup { PlacementTarget = popupButton, IsLightDismissEnabled = false, HorizontalOffset = 140, VerticalOffset = -180,
            Child = new Border { Width = 140, Height = 80, Background = Brushes.Lime } };
        popupButton.Click += (_, _) => popup.IsOpen = !popup.IsOpen;
        window.Content = new Border { Background = Brushes.Blue, Child = new StackPanel
        { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Children = { open, close, move, popupButton, popup } } };
        open.Click += (_, _) =>
        {
            occluder?.Close();
            occluder = new Window { Width = 160, Height = 120, WindowDecorations = WindowDecorations.None,
                ShowInTaskbar = false, Topmost = true, Background = Brushes.Red, Content = new Border { Background = Brushes.Red },
                Position = window.PointToScreen(new(180, 20)) };
            occluder.Show(window);
        };
        close.Click += (_, _) => { occluder?.Close(); occluder = null; };
        move.Click += (_, _) =>
        {
            var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
            if (screen is not null) window.Position = new(screen.Bounds.Right - (int)(window.ClientSize.Width * window.DesktopScaling / 2), screen.Bounds.Y + 60);
        };
        window.Closed += (_, _) => { occluder?.Close(); popup.IsOpen = false; };
        window.Opened += (_, _) =>
        {
            var screen = window.Screens.Primary;
            if (screen is not null) window.Position = new(screen.WorkingArea.X + 40, screen.WorkingArea.Y + 60);
        };
    }

    private static void ConfigureInputFixture(Window window)
    {
        var state = new TextBlock { Name = "InputState", Text = "idle" };
        var release = new TextBlock { Name = "ReleaseState", Text = "idle" };
        var keys = new TextBlock { Name = "KeyState", Text = "idle" };
        var result = new TextBlock { Name = "PickerState", Text = "idle" };
        var motion = new TextBlock { Name = "MotionState", Text = "idle" };
        var interruptDrag = false;
        var editor = new TextBox { Name = "NativeEditor", Text = "" };
        var focusNext = new TextBox { Name = "NativeFocusNext" };
        editor.KeyDown += (_, args) => keys.Text = $"{args.Key}:{args.KeyModifiers}";
        var pad = new Border { Name = "NativePad", Height = 100, Background = Brushes.LightGray };
        pad.PointerPressed += (_, args) =>
        {
            state.Text = $"{args.GetCurrentPoint(pad).Properties.PointerUpdateKind}:{args.ClickCount}:{args.KeyModifiers}";
            args.Pointer.Capture(pad);
            if (interruptDrag) { interruptDrag = false; OpenOtherWindow(); }
        };
        pad.AddHandler(InputElement.PointerReleasedEvent, (_, args) =>
            { release.Text = args.InitialPressMouseButton.ToString(); args.Pointer.Capture(null); }, handledEventsToo: true);
        pad.PointerMoved += (_, args) =>
        {
            var properties = args.GetCurrentPoint(pad).Properties;
            motion.Text = $"{properties.IsLeftButtonPressed}:{properties.IsMiddleButtonPressed}:{properties.IsRightButtonPressed}";
        };
        var other = new Button { Name = "OtherWindow", Content = "Open another window" };
        other.Click += (_, _) => OpenOtherWindow();
        void OpenOtherWindow()
        {
            var back = new Button { Name = "ReturnToMain", Content = "Return" };
            var child = new Window { Title = "Input ownership fixture", Width = 280, Height = 160, Content = back };
            back.Click += (_, _) => { child.Close(); window.Activate(); };
            child.Show();
            child.Activate();
        }
        var interrupt = new Button { Name = "ArmInterruptedDrag", Content = "Interrupt next drag with another window" };
        interrupt.Click += (_, _) => interruptDrag = true;
        var open = new Button { Name = "OpenFilePicker", Content = "Open file" };
        open.Click += async (_, _) =>
        {
            result.Text = "open";
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Owned native file picker", AllowMultiple = false });
            result.Text = files.Count == 0 ? "cancelled" : "selected:" + files[0].Name;
        };
        var save = new Button { Name = "SaveFilePicker", Content = "Save file" };
        save.Click += async (_, _) =>
        {
            result.Text = "open";
            var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Owned native save picker", SuggestedFileName = "native-save.txt" });
            result.Text = file is null ? "cancelled" : "selected:" + file.Name;
        };
        var prepared = new Button { Name = "PreparedPicker", Content = "Consume host-authorized test result" };
        prepared.Click += (_, _) =>
        {
#if ENABLE_UI_INSPECTION
            var bootstrap = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == "AvaScope.Bridge").GetType("AvaScope.Bridge.Bootstrap", true)!;
            var json = (string)bootstrap.GetMethod("TakePreparedPickerResult", [typeof(string)])!.Invoke(null, ["open-document"])!;
            using var response = System.Text.Json.JsonDocument.Parse(json);
            var status = response.RootElement.GetProperty("status").GetString();
            result.Text = status == "success" ? "predefined:" + Path.GetFileName(response.RootElement.GetProperty("selectedPath").GetString()) : "predefined:" + status;
#endif
        };
        window.Width = 520;
        window.Height = 580;
        window.Content = new StackPanel { Margin = new Thickness(16), Spacing = 6, Children = { state, release, keys, result, motion, pad, editor, focusNext, other, interrupt, open, save, prepared } };
#if TABLE_FIXTURE
        var table = new DataGrid { Name = "NativeTable", AutoGenerateColumns = false, Height = 360, Margin = new Thickness(16),
            ItemsSource = Enumerable.Range(0, 80).Select(index => new NativeTableRow("row-" + index, index % 2 == 0 ? "failed" : "passed")).ToArray() };
        table.Columns.Add(new DataGridTextColumn { Header = "Identifier", Binding = new Avalonia.Data.ReflectionBinding("Id"), IsReadOnly = true });
        table.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new Avalonia.Data.ReflectionBinding("Status") });
        window.Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/"))
            { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") });
        var inputPanel = (Control)window.Content;
        window.Content = null;
        Grid.SetColumn(table, 1);
        window.Width = 1000;
        window.Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Children = { inputPanel, table } };
#endif
        window.Opened += (_, _) => window.Activate();
    }

#if TABLE_FIXTURE
    private sealed class NativeTableRow(string id, string status)
    {
        public string Id { get; } = id;
        public string Status { get; set; } = status;
    }
#endif
}
