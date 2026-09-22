namespace AvaScope.Protocol;

public static class SemanticWaitConditionKinds
{
    public const string Exists = "exists";
    public const string Disappears = "disappears";
    public const string Visible = "visible";
    public const string Hidden = "hidden";
    public const string Enabled = "enabled";
    public const string Disabled = "disabled";
    public const string Checked = "checked";
    public const string Unchecked = "unchecked";
    public const string SelectedValue = "selected_value";
    public const string Text = "text";
    public const string Value = "value";
    public const string Rendered = "rendered";
    public const string CommandExecutable = "command_executable";
    public const string BindingValue = "binding_value";
    public const string TopLevelOpened = "top_level_opened";
    public const string TopLevelClosed = "top_level_closed";
    public const string ChangeFromBaseline = "change_from_baseline";
    public const string BridgeReady = "bridge_ready";
    public const string FrameReady = "frame_ready";
    public const string ApplicationReady = "application_ready";
    public const string ApplicationBusy = "application_busy";
    public const string LayoutStable = "layout_stable";
    public const string FrameStable = "frame_stable";

    public static bool IsReadiness(string? kind) => kind is BridgeReady or FrameReady
        or ApplicationReady or ApplicationBusy or LayoutStable or FrameStable;

    public static bool IsStability(string? kind) => kind is LayoutStable or FrameStable;

    public static bool IsTopLevelCondition(string? kind) => kind is TopLevelOpened or TopLevelClosed || IsReadiness(kind);

    public static IReadOnlyList<string> All { get; } =
    [
        Exists, Disappears, Visible, Hidden, Enabled, Disabled, Checked, Unchecked,
        SelectedValue, Text, Value, Rendered, CommandExecutable, BindingValue,
        TopLevelOpened, TopLevelClosed, ChangeFromBaseline,
        BridgeReady, FrameReady, ApplicationReady, ApplicationBusy, LayoutStable, FrameStable
    ];
}
