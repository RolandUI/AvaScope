using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace AvaScope.ComplexWorkflowApp;

// This fixture is also source-linked into the standalone host. It deliberately has
// no AvaScope dependency: its state journal is independent of the bridge response.
public partial class QaWindow : Window
{
    private readonly List<Window> _children = [];
    private readonly Queue<object> _events = new();
    private readonly DispatcherTimer _lease = new();
    private readonly string? _outputDirectory = Environment.GetEnvironmentVariable("AVASCOPE_QA_OUTPUT");
    private TextBox _editor = null!;
    private CancellationTokenSource? _loading;
    private bool _preparing;
    private bool _closed;
    private PixelRect? _screenBounds;
    private Size _requestedSceneSize = new(1120, 800);
    private int _resetGeneration;
    private int _toggleCount;
    private int _textChanges;
    private int _lowercaseCount;
    private int _uppercaseCount;
    private int _templateCount;
    private int _editorGeneration;
    private long _sequence;
    private string _locale = "en-US";

    public event Action<string>? ReadinessChanged;

    public QaWindow()
    {
        InitializeComponent();
        var leaseText = Environment.GetEnvironmentVariable("AVASCOPE_QA_LIFETIME_SECONDS") ?? "3600";
        if (!int.TryParse(leaseText, out var leaseSeconds) || leaseSeconds is < 30 or > 14400)
            throw new ArgumentException("AVASCOPE_QA_LIFETIME_SECONDS must be 30–14400.");
        _lease.Interval = TimeSpan.FromSeconds(leaseSeconds);
        _lease.Tick += (_, _) => { Record("lease_expired"); Close(); };
        _editor = DisplayNameEditor;
        ObserveEditor(_editor);
        Notifications.PropertyChanged += (_, change) =>
        {
            if (change.Property == ToggleButton.IsCheckedProperty && !_preparing)
            {
                _toggleCount++;
                Record("notifications_changed");
            }
        };
        NotesEditor.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBox.TextProperty) Record("notes_changed");
        };
        ResetButton.Click += (_, _) => ResetState();
        ThemeButton.Click += (_, _) =>
        {
            RequestedThemeVariant = ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
            Record("theme_changed");
        };
        LocaleButton.Click += (_, _) =>
        {
            _locale = _locale == "en-US" ? "hu-HU" : "en-US";
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(_locale);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(_locale);
            LocalizedHeading.Text = _locale == "hu-HU"
                ? "Megjelenítési referencia — a szöveg a kártyán belül marad"
                : "Rendering reference — correct text stays within its cards";
            Record("locale_changed");
        };
        SizeButton.Click += (_, _) =>
        {
            _requestedSceneSize = _requestedSceneSize.Width == 1120 ? new(1920, 1080) : new(1120, 800);
            Width = _requestedSceneSize.Width;
            Height = _requestedSceneSize.Height;
            Record("size_requested");
        };
        ReplaceEditorButton.Click += (_, _) => ReplaceEditor();
        DelayedButton.Click += async (_, _) => await LoadDataAsync();
        LowercaseButton.Click += (_, _) => { _lowercaseCount++; Record("lowercase_key"); };
        UppercaseButton.Click += (_, _) => { _uppercaseCount++; Record("uppercase_key"); };
        TemplateButton.Click += (_, _) => { _templateCount++; Record("template_click"); };
        Pages.SelectionChanged += (_, _) => Record("navigation");
        Rows.SelectionChanged += (_, _) => Record("row_selection");
        ChildButton.Click += (_, _) => OpenChild(false);
        ModalButton.Click += (_, _) => OpenChild(true);
        PopupButton.Click += (_, _) => QaPopup.IsOpen = true;
        ClosePopupButton.Click += (_, _) => QaPopup.IsOpen = false;
        QaPopup.Opened += (_, _) => Record("popup_opened");
        QaPopup.Closed += (_, _) => Record("popup_closed");
        SizeChanged += (_, _) => Record("size_observed");
        Opened += (_, _) => { _lease.Start(); Record("window_opened"); };
        Closed += (_, _) => { _closed = true; _lease.Stop(); CancelLoading(); CloseChildren(); Record("window_closed"); };
        ResetState();
    }

    public void ResetState()
    {
        _preparing = true;
        try
        {
            CancelLoading();
            CloseChildren();
            QaPopup.IsOpen = false;
            Notifications.IsChecked = false;
            _editor.Text = "Ada";
            _editor.SelectionStart = _editor.SelectionEnd = 0;
            NotesEditor.Text = "First line\nÁrvíztűrő tükörfúrógép 😀";
            Rows.ItemsSource = Enumerable.Range(1, 200).Select(index => $"Record {index:000} — seeded QA data").ToArray();
            Rows.SelectedIndex = -1;
            Pages.SelectedIndex = 0;
            RequestedThemeVariant = ThemeVariant.Light;
            _locale = "en-US";
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(_locale);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(_locale);
            LocalizedHeading.Text = "Rendering reference — correct text stays within its cards";
            _requestedSceneSize = new(1120, 800);
            Width = _requestedSceneSize.Width;
            Height = _requestedSceneSize.Height;
            LoadStatus.Text = "Ready";
            WindowStatus.Text = "No secondary windows";
            _toggleCount = _textChanges = _lowercaseCount = _uppercaseCount = _templateCount = 0;
            _resetGeneration++;
        }
        finally { _preparing = false; }
        ReadinessChanged?.Invoke("ready");
        Record("reset");
    }

    public void CleanupState()
    {
        CancelLoading();
        CloseChildren();
        QaPopup.IsOpen = false;
        Record("fixture_cleanup");
    }

    private void ObserveEditor(TextBox editor)
    {
        editor.PropertyChanged += (_, change) =>
        {
            if (ReferenceEquals(editor, _editor) && change.Property == TextBox.TextProperty && !_preparing)
            {
                _textChanges++;
                Record("display_name_changed");
            }
        };
    }

    private void ReplaceEditor()
    {
        var replacement = new TextBox { Text = _editor.Text, PlaceholderText = "Display name", Width = 360 };
        AutomationProperties.SetAutomationId(replacement, "qa-display-name");
        EditorHost.Children.Clear();
        _editor = replacement;
        ObserveEditor(replacement);
        EditorHost.Children.Add(replacement);
        _editorGeneration++;
        Record("editor_replaced");
    }

    private async Task LoadDataAsync()
    {
        CancelLoading();
        var loading = new CancellationTokenSource();
        _loading = loading;
        LoadStatus.Text = "Loading";
        ReadinessChanged?.Invoke("busy");
        Record("load_started");
        try
        {
            await Task.Delay(450, loading.Token);
            if (!ReferenceEquals(loading, _loading)) return;
            LoadStatus.Text = "Loaded 200 records";
            ReadinessChanged?.Invoke("ready");
            Record("load_completed");
        }
        catch (OperationCanceledException) when (loading.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(loading, _loading)) _loading = null;
            loading.Dispose();
        }
    }

    private void CancelLoading()
    {
        _loading?.Cancel();
        _loading = null;
    }

    private void OpenChild(bool modal)
    {
        var close = new Button { Content = "Close", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        AutomationProperties.SetAutomationId(close, modal ? "qa-close-modal" : "qa-close-child");
        var child = new Window
        {
            Title = modal ? "AvaScope QA confirmation" : "AvaScope QA details",
            Width = 460, Height = 260,
            Content = new StackPanel
            {
                Margin = new Thickness(20), Spacing = 14,
                Children = { new TextBlock { Text = $"Profile: {_editor.Text}", FontSize = 22 },
                    new TextBlock { Text = $"Notifications: {Notifications.IsChecked == true}" }, close }
            }
        };
        _children.Add(child);
        close.Click += (_, _) => child.Close();
        child.Closed += (_, _) => { _children.Remove(child); Record("child_closed"); };
        if (modal) _ = child.ShowDialog(this);
        else child.Show(this);
        WindowStatus.Text = modal ? "Confirmation opened" : "Details opened";
        Record(modal ? "modal_opened" : "child_opened");
    }

    private void CloseChildren()
    {
        foreach (var child in _children.ToArray()) child.Close();
    }

    private void Record(string action)
    {
        if (_preparing) return;
        _sequence++;
        var entry = new { sequence = _sequence, action, timestamp = DateTimeOffset.UtcNow };
        _events.Enqueue(entry);
        while (_events.Count > 100) _events.Dequeue();
        IdentityStatus.Text = $"a={_lowercaseCount}; A={_uppercaseCount}";
        EnvironmentLabel.Text = FormattableString.Invariant($"{RenderScaling:0.##}× · {ClientSize.Width:0}×{ClientSize.Height:0} DIP · {_locale}");
        StateSummary.Text = $"reset={_resetGeneration}; notifications={Notifications.IsChecked == true}; toggles={_toggleCount}; name={Bound(_editor.Text)}; edits={_textChanges}; a={_lowercaseCount}; A={_uppercaseCount}";
        if (string.IsNullOrWhiteSpace(_outputDirectory)) return;
        if (!_closed) _screenBounds = Screens.ScreenFromWindow(this)?.Bounds;
        Directory.CreateDirectory(_outputDirectory);
        var state = new
        {
            schemaVersion = 1, fixture = "agent-qa", fixtureVersion = "1", seed = 42,
            processId = Environment.ProcessId, timestamp = DateTimeOffset.UtcNow, sequence = _sequence,
            resetGeneration = _resetGeneration, editorGeneration = _editorGeneration,
            notifications = Notifications.IsChecked == true, toggleCount = _toggleCount,
            displayName = Bound(_editor.Text), textChanges = _textChanges, notes = Bound(NotesEditor.Text),
            lowercaseCount = _lowercaseCount, uppercaseCount = _uppercaseCount, templateCount = _templateCount,
            selectedPage = Pages.SelectedIndex, selectedRow = Rows.SelectedIndex, rowCount = Rows.ItemCount,
            loadStatus = LoadStatus.Text, childWindows = _children.Count, popupOpen = QaPopup.IsOpen,
            theme = ActualThemeVariant.ToString(), locale = _locale, font = FontManager.Current.DefaultFontFamily.Name,
            nativeHandleKind = TryGetPlatformHandle()?.HandleDescriptor, renderScaling = RenderScaling,
            clientWidth = ClientSize.Width, clientHeight = ClientSize.Height,
            physicalWidth = (int)Math.Ceiling(ClientSize.Width * RenderScaling), physicalHeight = (int)Math.Ceiling(ClientSize.Height * RenderScaling),
            requestedWidth = _requestedSceneSize.Width, requestedHeight = _requestedSceneSize.Height, windowX = Position.X, windowY = Position.Y,
            screenWidth = _screenBounds?.Width, screenHeight = _screenBounds?.Height,
            leaseSeconds = _lease.Interval.TotalSeconds,
            avaloniaVersion = typeof(Application).Assembly.GetName().Version?.ToString(),
            events = _events.ToArray()
        };
        var path = Path.Combine(_outputDirectory, "qa-state.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    private static string? Bound(string? value) => value?.Length > 2048 ? value[..2048] : value;
}
