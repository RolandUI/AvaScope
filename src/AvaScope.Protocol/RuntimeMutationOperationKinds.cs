namespace AvaScope.Protocol;

public static class RuntimeMutationOperationKinds
{
    public const string NoOp = "noop";
    public const string SetProperty = "set_property";
    public const string AddClass = "add_class";
    public const string RemoveClass = "remove_class";
    public const string ToggleClass = "toggle_class";
    public const string SetResource = "set_resource";
    public const string RemoveResource = "remove_resource";
}
