namespace AvaScope.Protocol;

public static class BridgeIpcMethods
{
    public const string Health = "health";
    public const string Capabilities = "capabilities";
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
}
