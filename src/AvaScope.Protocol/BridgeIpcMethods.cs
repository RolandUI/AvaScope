namespace AvaScope.Protocol;

public static class BridgeIpcMethods
{
    public const string Health = "health";
    public const string Capabilities = "capabilities";
    public const string Readiness = "readiness";
    public const string Observe = "observe";
    public const string ExplainAction = "explain_action";
    public const string ActionMap = "action_map";
    public const string InspectFocus = "inspect_focus";
    public const string ProbeFocus = "probe_focus";
    public const string EvaluateRuntime = "evaluate_runtime";
    public const string Operation = "operation";
    public const string Trace = "trace";
    public const string EditText = "edit_text";
    public const string Scene = "scene";
    public const string EnsureState = "ensure_state";
    public const string InspectForm = "inspect_form";
    public const string FillForm = "fill_form";
    public const string QueryTable = "query_table";
    public const string TableAction = "table_action";
    public const string ObserveChanges = "observe_changes";
    public const string VirtualItem = "virtual_item";
    public const string NativePicker = "native_picker";
    public const string SessionControl = "session_control";
    public const string ListTopLevels = "list_top_levels";
    public const string Screenshot = "screenshot";
    public const string VisualTree = "visual_tree";
    public const string LogicalTree = "logical_tree";
    public const string InspectNode = "inspect_node";
    public const string ExplainLayout = "explain_layout";
    public const string FindNodes = "find_nodes";
    public const string Input = "input";
    public const string ValidateInput = "validate_input";
    public const string MutateNode = "mutate_node";
    public const string ValidateMutation = "validate_mutation";
    public const string MutationReview = "mutation_review";
    public const string CustomActions = "custom_actions";
    public const string InvokeCustomAction = "invoke_custom_action";
    public const string CloseSession = "close_session";

    public static IReadOnlyList<string> All { get; } =
    [
        Health,
        Capabilities,
        Readiness,
        Observe,
        ExplainAction,
        ActionMap,
        InspectFocus,
        ProbeFocus,
        EvaluateRuntime,
        Operation,
        Trace,
        EditText,
        Scene,
        EnsureState,
        InspectForm,
        FillForm,
        QueryTable,
        TableAction,
        ObserveChanges,
        VirtualItem,
        NativePicker,
        SessionControl,
        ListTopLevels,
        Screenshot,
        VisualTree,
        LogicalTree,
        InspectNode,
        ExplainLayout,
        FindNodes,
        Input,
        ValidateInput,
        MutateNode,
        ValidateMutation,
        MutationReview,
        CustomActions,
        InvokeCustomAction,
        CloseSession
    ];

    public static bool RequiresControl(BridgeIpcRequest request) => request.Method is Input or MutateNode or InvokeCustomAction or CloseSession or EnsureState or FillForm or TableAction or ProbeFocus
        || request.Method == Operation && request.Operation?.Action == "cancel"
        || request.Method == Trace && request.Trace?.Action is "start" or "stop"
        || request.Method == EditText && request.TextEdit?.Action != "read"
        || request.Method == Scene && request.Scene?.Action != "inspect"
        || request.Method == VirtualItem && request.VirtualItem?.Action != "find"
        || request.Method == NativePicker && request.NativePicker?.Operation != "detect";
}
