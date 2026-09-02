namespace AvaScope.Protocol;

public static class SemanticWorkflowActions
{
    public const string Click = "click";
    public const string TypeText = "type_text";
    public const string ClearText = "clear_text";
    public const string Focus = "focus";
    public const string Invoke = "invoke";
    public const string Select = "select";
    public const string Toggle = "toggle";
    public const string Expand = "expand";
    public const string Collapse = "collapse";
    public const string KeyDown = "key_down";
    public const string KeyUp = "key_up";
    public const string Drag = "drag";
    public const string Swipe = "swipe";
    public const string LongPress = "long_press";
    public const string PressAndHold = "press_and_hold";
    public const string AssertState = "assert_state";
    public const string Screenshot = "screenshot";
    public const string Inspect = "inspect";
    public const string Wait = "wait";
    public const string WaitForNode = "wait_for_node";
    public const string WaitForState = "wait_for_state";
    public const string WaitForDialog = "wait_for_dialog";
    public const string ValidateAction = "validate_action";
    public const string ValidateMutation = "validate_mutation";
    public const string PickerResult = "picker_result";

    public static IReadOnlyList<string> All { get; } =
    [
        Click, TypeText, ClearText, Focus, Invoke, Select, Toggle, Expand,
        Collapse, KeyDown, KeyUp, Drag, Swipe, LongPress, PressAndHold,
        AssertState, Screenshot, Inspect, Wait,
        WaitForNode, WaitForState, WaitForDialog, ValidateAction,
        ValidateMutation, PickerResult
    ];
}
