using System.Globalization;
using System.ComponentModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace AvaScope.ComplexWorkflowApp;

public sealed record QaSceneItem(string Id, string Label, Rect Bounds);

public sealed class QaSceneControl : Control
{
    public IReadOnlyList<QaSceneItem> Items { get; } =
    [new("blue", "Blue record", new(10, 20, 150, 80)),
     new("orange", "Orange record", new(195, 20, 150, 80)),
     new("green", "Green record", new(380, 20, 150, 80))];
    public int Generation { get; private set; }
    public int Revision { get; private set; }
    public int SelectionCount { get; private set; }
    public string? SelectedId { get; private set; }
    public int Offset { get; private set; }
    public Matrix SceneToCanvas => Matrix.CreateTranslation(Offset, 0);
    public event Action? Changed;

    public QaSceneControl()
    {
        PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            var point = args.GetPosition(this) - new Vector(Offset, 0);
            var item = Items.FirstOrDefault(item => item.Bounds.Contains(point));
            if (item is not null) Select(item.Id);
        };
    }

    public bool Select(string id)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!Items.Any(item => item.Id == id)) return false;
        SelectedId = id; SelectionCount++; Revision++;
        InvalidateVisual(); Changed?.Invoke(); return true;
    }

    public void Shift()
    {
        Dispatcher.UIThread.VerifyAccess();
        Offset = Offset == 0 ? 20 : 0; Revision++;
        InvalidateVisual(); Changed?.Invoke();
    }

    public void Reset()
    {
        Dispatcher.UIThread.VerifyAccess();
        Generation++; Revision++; SelectionCount = 0; SelectedId = null; Offset = 0;
        InvalidateVisual(); Changed?.Invoke();
    }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(Bounds.Size));
        using (context.PushTransform(SceneToCanvas))
            foreach (var item in Items)
            {
                var brush = item.Id switch { "blue" => Brushes.SteelBlue, "orange" => Brushes.DarkOrange, _ => Brushes.SeaGreen };
                context.DrawRectangle(brush, new Pen(SelectedId == item.Id ? Brushes.Black : Brushes.Transparent, 4), item.Bounds);
                var text = new FormattedText(item.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    Typeface.Default, 18, Brushes.White);
                context.DrawText(text, item.Bounds.TopLeft + new Point(12, 26));
            }
    }
}

public sealed class QaRecord(string id, string name, string status, int score) : INotifyPropertyChanged
{
    private string _status = status;
    private int _score = score;
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Status
    {
        get => _status;
        set { if (_status == value) return; _status = value; PropertyChanged?.Invoke(this, new(nameof(Status))); }
    }
    public int Score
    {
        get => _score;
        set { if (_score == value) return; _score = value; PropertyChanged?.Invoke(this, new(nameof(Score))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

// Reproduces AvaloniaUI/Avalonia#20693 without private APIs or external assets.
public sealed class QaOpacityTransformControl : Control
{
    public QaOpacityTransformControl() => RenderOptions.SetRequiresFullOpacityHandling(this, true);

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(Brushes.White, null, new Rect(Bounds.Size));
        for (var row = 0; row < 5; row++)
        {
            using (context.PushOpacity(0.9))
            using (context.PushTransform(Matrix.CreateScale(1.5, 1.5)))
                context.DrawText(new FormattedText("MM", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    Typeface.Default, 12, Brushes.Black), new Point(10, 10 + row * 30));
        }
        using (context.PushOpacity(0.5))
        {
            context.DrawRectangle(Brushes.Red, null, new Rect(120, 20, 30, 20));
            context.DrawRectangle(Brushes.Blue, null, new Rect(130, 20, 30, 20));
        }
        using (context.PushClip(new Rect(120, 60, 20, 20)))
            context.DrawRectangle(Brushes.Lime, null, new Rect(110, 50, 40, 40));
    }
}

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
    private QaRecord[] _records = [];
    private int _formSaves;
    private int _tableEdits;
    private int _menuActions;
    private int _contextActions;
    private int _pointerPresses;
    private int _pointerReleases;
    private int _drags;
    private int _keyDowns;
    private int _focusActions;
    private string? _lastKey;
    private Point? _pointerStart;
    private IPointer? _capturedPointer;
    private Point _dragDelta;
    private object? _savedProfile;
    private CancellationTokenSource? _operation;
    private IDisposable? _diagnosticBinding;
    private int _operationStarts;
    private int _operationCompletions;
    private int _operationFailures;
    private int _operationCancellations;
    private string _operationState = "idle";
    private bool _declaredRuntime;
    private CancellationTokenSource? _animation;
    private int _animationStarts;
    private int _animationStops;
    private string? _animationError;

    public event Action<string>? ReadinessChanged;

    public QaWindow()
    {
        // Implicit control themes must be available when XAML attaches the controls.
        Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/"))
            { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") });
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
        StartAnimationButton.Click += async (_, _) => await RunAnimationAsync();
        StopAnimationButton.Click += (_, _) => { StopAnimation(); Record("animation_stopped"); };
        SampleAnimationButton.Click += (_, _) => Record("animation_sampled");
        Pages.SelectionChanged += (_, _) => Record("navigation");
        Rows.SelectionChanged += (_, _) => Record("row_selection");
        ChildButton.Click += (_, _) => OpenChild(false);
        ModalButton.Click += (_, _) => OpenChild(true);
        PopupButton.Click += (_, _) => QaPopup.IsOpen = true;
        ClosePopupButton.Click += (_, _) => QaPopup.IsOpen = false;
        QaPopup.Opened += (_, _) => Record("popup_opened");
        QaPopup.Closed += (_, _) => Record("popup_closed");
        FormRole.ItemsSource = new[] { "Reader", "Editor", "Reviewer" };
        foreach (var field in new Control[] { FormName, FormEmail, FormPassword, FormConsent, FormPriority, FormRole })
            field.PropertyChanged += (_, change) =>
            {
                if (change.Property == TextBox.TextProperty || change.Property == ToggleButton.IsCheckedProperty
                    || change.Property == RangeBase.ValueProperty || change.Property == SelectingItemsControl.SelectedIndexProperty)
                    Record("form_changed");
            };
        SubmitFormButton.Click += (_, _) => SubmitForm();
        RecordsTable.SelectionChanged += (_, _) => Record("table_selection");
        RecordsTable.Sorting += (_, _) => Dispatcher.UIThread.Post(() => { if (!_closed) Record("table_sort"); });
        MenuApply.Click += (_, _) => { _menuActions++; Record("menu_action"); };
        ContextApply.Click += (_, _) => { _contextActions++; Record("context_action"); };
        PointerPad.PointerPressed += (_, args) =>
        {
            if (!args.GetCurrentPoint(PointerPad).Properties.IsLeftButtonPressed) return;
            _pointerStart = args.GetPosition(PointerPad);
            _capturedPointer = args.Pointer;
            _pointerPresses++;
            args.Pointer.Capture(PointerPad);
            PointerPad.Focus();
            Record("pointer_pressed");
        };
        PointerPad.PointerReleased += (_, args) =>
        {
            if (_pointerStart is not { } start || args.InitialPressMouseButton != MouseButton.Left) return;
            var delta = args.GetPosition(PointerPad) - start;
            _dragDelta = new Point(delta.X, delta.Y);
            if (delta.X * delta.X + delta.Y * delta.Y >= 400) _drags++;
            _pointerStart = null;
            _pointerReleases++;
            args.Pointer.Capture(null);
            Record("pointer_released");
        };
        PointerPad.PointerCaptureLost += (_, _) => { _pointerStart = null; _capturedPointer = null; };
        KeyboardEditor.KeyDown += (_, args) =>
        {
            _keyDowns++; _lastKey = $"{args.KeyModifiers}:{args.Key}"; Record("key_down");
        };
        KeyboardEditor.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBox.TextProperty) Record("keyboard_text");
        };
        KeyboardNext.Click += (_, _) => { _focusActions++; Record("focus_action"); };
        RuntimeScene.Changed += () => Record("scene_changed");
        MoveSceneButton.Click += (_, _) => RuntimeScene.Shift();
        StartOperationButton.Click += async (_, _) => await RunFixtureOperationAsync("complete", 8);
        FailOperationButton.Click += async (_, _) => await RunFixtureOperationAsync("fail", 8);
        CancelOperationButton.Click += (_, _) => { _operation?.Cancel(); Record("operation_cancel_requested"); };
        DiagnosticToggle.PropertyChanged += (_, change) =>
        {
            if (change.Property == ToggleButton.IsCheckedProperty) SetDiagnosticErrors(DiagnosticToggle.IsChecked == true);
        };
        SizeChanged += (_, _) => Record("size_observed");
        Opened += (_, _) => { _lease.Start(); Record("window_opened"); };
        Closing += (_, _) => StopAnimation();
        Closed += (_, _) => { _closed = true; _lease.Stop(); CancelLoading(); StopFixtureOperation(); StopAnimation(); _diagnosticBinding?.Dispose(); CloseChildren(); Record("window_closed"); };
        ResetState();
    }

    public void ResetState()
    {
        _preparing = true;
        try
        {
            CancelLoading();
            StopFixtureOperation();
            StopAnimation();
            CloseChildren();
            QaPopup.IsOpen = false;
            Notifications.IsChecked = false;
            _editor.Text = "Ada";
            _editor.SelectionStart = _editor.SelectionEnd = 0;
            NotesEditor.Text = "First line\nÁrvíztűrő tükörfúrógép 😀";
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
            FormName.Text = "Ada Lovelace";
            FormEmail.Text = "ada@example.test";
            FormPassword.Text = "fixture-only-password";
            FormConsent.IsChecked = false;
            FormPriority.Value = 20;
            FormRole.SelectedIndex = 0;
            DataValidationErrors.SetErrors(FormName, null);
            DataValidationErrors.SetErrors(FormEmail, null);
            FormStatus.Text = "Not saved";
            _savedProfile = null;
            _formSaves = _tableEdits = 0;
            _records = Enumerable.Range(1, 200).Select(index => new QaRecord($"QA-{index:000}",
                $"Record {index:000}", index % 2 == 0 ? "passed" : "pending", index % 101)).ToArray();
            Rows.ItemsSource = _records;
            Rows.SelectedIndex = -1;
            foreach (var row in _records)
                row.PropertyChanged += (_, _) =>
                {
                    // Detached rows must not affect the new seed after reset.
                    if (_records.Contains(row)) { _tableEdits++; Record("table_edit"); }
                };
            RecordsTable.ItemsSource = new DataGridCollectionView(_records);
            RecordsTable.SelectedItem = null;
            _menuActions = _contextActions = _pointerPresses = _pointerReleases = _drags = _keyDowns = _focusActions = 0;
            _lastKey = null;
            _pointerStart = null;
            _dragDelta = default;
            PointerPad.ContextMenu?.Close();
            InputMenu.Close();
            _capturedPointer?.Capture(null);
            KeyboardEditor.Text = "Keyboard seed — Árvíztűrő 😀";
            RuntimeScene.Reset();
            _operationStarts = _operationCompletions = _operationFailures = _operationCancellations = 0;
            _operationState = "idle";
            _animationStarts = _animationStops = 0;
            _animationError = null;
            AnimationStatus.Text = "Idle";
            OperationProgress.Value = 0;
            OperationStatus.Text = "Idle";
            DiagnosticToggle.IsChecked = false;
            SetDiagnosticErrors(false);
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
        StopFixtureOperation();
        StopAnimation();
        CloseChildren();
        QaPopup.IsOpen = false;
        PointerPad.ContextMenu?.Close();
        InputMenu.Close();
        _capturedPointer?.Capture(null);
        Record("fixture_cleanup");
    }

    public void EnableDeclaredRuntime()
    {
        _declaredRuntime = true;
        DeclarationStatus.Text = "Scene selection and cancellable work are declared to AvaScope by this host.";
        Record("runtime_declared");
    }

    public async Task RunAnimationAsync()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_animation is not null || _closed) return;
        var cancellation = new CancellationTokenSource();
        _animation = cancellation;
        _animationStarts++;
        _animationError = null;
        AnimationStatus.Text = "Running";
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(2), IterationCount = new IterationCount(600),
            PlaybackDirection = PlaybackDirection.Alternate, FillMode = FillMode.None,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Border.BackgroundProperty, Brushes.Blue) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Border.BackgroundProperty, Brushes.DodgerBlue) } }
            }
        };
        try
        {
            var runs = new[] { AnimationStyleTarget, AnimationStyleReference, AnimationLocalTarget, AnimationLocalReference }
                .Select(control => animation.RunAsync(control, cancellation.Token)).ToArray();
            Record("animation_started");
            await Task.WhenAll(runs);
            if (ReferenceEquals(_animation, cancellation) && !_closed)
            {
                _animation = null;
                AnimationStatus.Text = "Completed";
                Record("animation_completed");
            }
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(_animation, cancellation) && !_closed)
            {
                StopAnimation();
                _animationError = Bound(exception.Message);
                AnimationStatus.Text = "Failed";
                Record("animation_failed");
            }
        }
        finally { cancellation.Dispose(); }
    }

    private void StopAnimation()
    {
        var animation = _animation;
        _animation = null;
        if (animation is null) return;
        _animationStops++;
        // Detach ownership before cancellation so an old continuation cannot overwrite reset/cleanup.
        animation.Cancel();
        AnimationStatus.Text = "Stopped";
    }

    public async Task<string> RunFixtureOperationAsync(string mode, int steps,
        CancellationToken cancellationToken = default, Action<double, string>? report = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (mode is not ("complete" or "fail") || steps is < 1 or > 10)
            throw new ArgumentException("Fixture work requires complete/fail and 1–10 steps.");
        StopFixtureOperation();
        var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _operation = operation;
        _operationStarts++; _operationState = "running";
        OperationProgress.Value = 0; OperationStatus.Text = "Running";
        Record("operation_started");
        try
        {
            for (var step = 1; step <= steps; step++)
            {
                await Task.Delay(150, operation.Token);
                if (!ReferenceEquals(operation, _operation) || _closed) return "cancelled";
                OperationProgress.Value = 100d * step / steps;
                OperationStatus.Text = $"Running {step}/{steps}";
                report?.Invoke((double)step / steps, OperationStatus.Text);
                Record("operation_progress");
            }
            _operationState = mode == "fail" ? "failed" : "completed";
            if (mode == "fail") _operationFailures++; else _operationCompletions++;
            OperationStatus.Text = mode == "fail" ? "Deliberate fixture failure" : "Completed";
            Record("operation_" + _operationState);
            return _operationState;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            // Reset, replacement and cleanup own their new state; old work cannot overwrite it.
            if (ReferenceEquals(operation, _operation) && !_closed)
            {
                _operationCancellations++; _operationState = "cancelled";
                OperationStatus.Text = "Cancelled";
                Record("operation_cancelled");
            }
            return "cancelled";
        }
        finally
        {
            if (ReferenceEquals(operation, _operation)) _operation = null;
            operation.Dispose();
        }
    }

    private void StopFixtureOperation()
    {
        var operation = _operation;
        _operation = null;
        operation?.Cancel();
        if (_operationState == "running")
        {
            _operationState = "cancelled";
            OperationStatus.Text = "Cancelled";
        }
    }

    private void SetDiagnosticErrors(bool enabled)
    {
        _diagnosticBinding?.Dispose(); _diagnosticBinding = null;
        DataValidationErrors.SetErrors(DiagnosticEditor, enabled ? new[] { "Intentional QA validation error." } : null);
        DiagnosticBinding.Text = "Binding fixture disabled";
        if (enabled)
            _diagnosticBinding = DiagnosticBinding.Bind(TextBlock.TextProperty,
                new ReflectionBinding("MissingQaDiagnosticProperty") { Source = this, FallbackValue = "Expected missing binding" });
        DiagnosticLayout.Width = enabled ? 800 : 260;
        DiagnosticLayout.Text = enabled ? "Intentional clipped layout — this text exceeds its declared parent" : "Layout fixture reference";
        Record("diagnostics_changed");
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

    private void SubmitForm()
    {
        var missingName = string.IsNullOrWhiteSpace(FormName.Text);
        var email = FormEmail.Text ?? string.Empty;
        var invalidEmail = !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email;
        DataValidationErrors.SetErrors(FormName, missingName ? new[] { "Contact name is required." } : null);
        DataValidationErrors.SetErrors(FormEmail, invalidEmail ? new[] { "Enter a valid email address." } : null);
        if (missingName || invalidEmail)
        {
            FormStatus.Text = "Validation failed — profile not saved";
            Record("form_rejected");
            return;
        }
        _formSaves++;
        _savedProfile = new { name = Bound(FormName.Text), email = Bound(email), role = FormRole.SelectedItem,
            consent = FormConsent.IsChecked, priority = FormPriority.Value };
        FormStatus.Text = $"Saved {_formSaves}: {FormName.Text} · {FormRole.SelectedItem} · priority {FormPriority.Value:0}";
        Record("form_saved");
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
        TableStatus.Text = $"selected={(RecordsTable.SelectedItem as QaRecord)?.Id ?? "none"}; edits={_tableEdits}";
        InputStatus.Text = $"menu={_menuActions}; context={_contextActions}; presses={_pointerPresses}; releases={_pointerReleases}; drags={_drags}; delta={_dragDelta}; keys={_keyDowns}; last={_lastKey ?? "none"}; focus actions={_focusActions}";
        SceneStatus.Text = $"selected={RuntimeScene.SelectedId ?? "none"}; revision={RuntimeScene.Revision}; selections={RuntimeScene.SelectionCount}";
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
            selectedRowKey = (Rows.SelectedItem as QaRecord)?.Id,
            loadStatus = LoadStatus.Text, childWindows = _children.Count, popupOpen = QaPopup.IsOpen,
            form = new { name = Bound(FormName.Text), email = Bound(FormEmail.Text), role = FormRole.SelectedItem,
                consent = FormConsent.IsChecked, priority = FormPriority.Value, saves = _formSaves,
                nameInvalid = DataValidationErrors.GetHasErrors(FormName), emailInvalid = DataValidationErrors.GetHasErrors(FormEmail),
                status = Bound(FormStatus.Text), savedProfile = _savedProfile },
            table = new { count = _records.Length, selectedKey = (RecordsTable.SelectedItem as QaRecord)?.Id,
                edits = _tableEdits, rows = _records.Select(row => new { id = row.Id, name = row.Name, status = Bound(row.Status), score = row.Score }),
                viewOrder = RecordsTable.ItemsSource?.Cast<QaRecord>().Select(row => row.Id).ToArray() },
            input = new { menuActions = _menuActions, contextActions = _contextActions, pointerPresses = _pointerPresses,
                pointerReleases = _pointerReleases, drags = _drags, dragX = _dragDelta.X, dragY = _dragDelta.Y,
                keyDowns = _keyDowns, lastKey = _lastKey, text = Bound(KeyboardEditor.Text), focusActions = _focusActions },
            scene = new { declared = _declaredRuntime, generation = RuntimeScene.Generation, revision = RuntimeScene.Revision,
                selectedId = RuntimeScene.SelectedId, selections = RuntimeScene.SelectionCount, offset = RuntimeScene.Offset,
                objects = RuntimeScene.Items.Select(item => new { id = item.Id, label = item.Label,
                    x = item.Bounds.X, y = item.Bounds.Y, width = item.Bounds.Width, height = item.Bounds.Height }) },
            operation = new { state = _operationState, starts = _operationStarts, completions = _operationCompletions,
                failures = _operationFailures, cancellations = _operationCancellations, progress = OperationProgress.Value,
                status = OperationStatus.Text },
            animation = new { running = _animation is not null, starts = _animationStarts, stops = _animationStops,
                status = AnimationStatus.Text, error = _animationError,
                controls = new[] { AnimationStyleTarget, AnimationStyleReference, AnimationLocalTarget, AnimationLocalReference }
                    .Select(control => new { name = control.Name, background = control.Background?.ToString(),
                        isAnimating = control.IsAnimating(Border.BackgroundProperty),
                        priority = control.GetDiagnostic(Border.BackgroundProperty).Priority.ToString() }) },
            intentionalDiagnostics = new { enabled = DiagnosticToggle.IsChecked == true,
                validationError = DataValidationErrors.GetHasErrors(DiagnosticEditor), bindingAttached = _diagnosticBinding is not null,
                childWidth = DiagnosticLayout.Width },
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
