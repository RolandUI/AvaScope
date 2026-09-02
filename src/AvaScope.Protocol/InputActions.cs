namespace AvaScope.Protocol;

public static class InputActions
{
    public const string PointerMove = "pointer_move";
    public const string PointerDown = "pointer_down";
    public const string PointerUp = "pointer_up";
    public const string Click = "click";
    public const string KeyText = "key_text";
    public const string ClearText = "clear_text";
    public const string Focus = "focus";
    public const string KeyDown = "key_down";
    public const string KeyUp = "key_up";
    public const string Invoke = "invoke";
    public const string Select = "select";
    public const string Toggle = "toggle";
    public const string Expand = "expand";
    public const string Collapse = "collapse";
    public const string Scroll = "scroll";
    public const string Drag = "drag";
    public const string Swipe = "swipe";
    public const string LongPress = "long_press";
    public const string PressAndHold = "press_and_hold";

    public static IReadOnlyList<string> All { get; } =
    [
        PointerMove, PointerDown, PointerUp, Click, KeyText, ClearText, Focus,
        KeyDown, KeyUp, Invoke, Select, Toggle, Expand, Collapse, Scroll,
        Drag, Swipe, LongPress, PressAndHold
    ];

    public static bool IsGesture(string action) =>
        action is Drag or Swipe or LongPress or PressAndHold;
}
