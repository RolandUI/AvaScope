using System.Text.Json.Serialization;
using AvaScope.Protocol;

namespace AvaScope.Mcp;

[JsonConverter(typeof(JsonStringEnumConverter<McpInputAction>))]
public enum McpInputAction
{
    [JsonStringEnumMemberName(InputActions.PointerMove)] PointerMove,
    [JsonStringEnumMemberName(InputActions.PointerDown)] PointerDown,
    [JsonStringEnumMemberName(InputActions.PointerUp)] PointerUp,
    [JsonStringEnumMemberName(InputActions.Click)] Click,
    [JsonStringEnumMemberName(InputActions.KeyText)] KeyText,
    [JsonStringEnumMemberName(InputActions.ClearText)] ClearText,
    [JsonStringEnumMemberName(InputActions.Focus)] Focus,
    [JsonStringEnumMemberName(InputActions.KeyDown)] KeyDown,
    [JsonStringEnumMemberName(InputActions.KeyUp)] KeyUp,
    [JsonStringEnumMemberName(InputActions.Invoke)] Invoke,
    [JsonStringEnumMemberName(InputActions.Select)] Select,
    [JsonStringEnumMemberName(InputActions.Toggle)] Toggle,
    [JsonStringEnumMemberName(InputActions.Expand)] Expand,
    [JsonStringEnumMemberName(InputActions.Collapse)] Collapse,
    [JsonStringEnumMemberName(InputActions.Scroll)] Scroll
}

[JsonConverter(typeof(JsonStringEnumConverter<McpDiagnosticsMode>))]
public enum McpDiagnosticsMode
{
    [JsonStringEnumMemberName("all")] All,
    [JsonStringEnumMemberName("active-only")] ActiveOnly,
    [JsonStringEnumMemberName("minimal")] Minimal,
    [JsonStringEnumMemberName("json-minimal")] JsonMinimal
}

[JsonConverter(typeof(JsonStringEnumConverter<McpMutationOperation>))]
public enum McpMutationOperation
{
    [JsonStringEnumMemberName(RuntimeMutationOperationKinds.NoOp)] NoOp,
    [JsonStringEnumMemberName(RuntimeMutationOperationKinds.SetProperty)] SetProperty,
    [JsonStringEnumMemberName(RuntimeMutationOperationKinds.AddClass)] AddClass,
    [JsonStringEnumMemberName(RuntimeMutationOperationKinds.RemoveClass)] RemoveClass,
    [JsonStringEnumMemberName(RuntimeMutationOperationKinds.ToggleClass)] ToggleClass,
    [JsonStringEnumMemberName(RuntimeMutationOperationKinds.SetResource)] SetResource,
    [JsonStringEnumMemberName(RuntimeMutationOperationKinds.RemoveResource)] RemoveResource,
    [JsonStringEnumMemberName(RuntimeMutationOperationKinds.ResetMutation)] ResetMutation,
    [JsonStringEnumMemberName(RuntimeMutationOperationKinds.ResetAll)] ResetAll
}

[JsonConverter(typeof(JsonStringEnumConverter<McpMinimumSeverity>))]
public enum McpMinimumSeverity
{
    [JsonStringEnumMemberName("all")] All,
    [JsonStringEnumMemberName(PreviewDiagnosticSeverities.Info)] Info,
    [JsonStringEnumMemberName(PreviewDiagnosticSeverities.Warning)] Warning,
    [JsonStringEnumMemberName(PreviewDiagnosticSeverities.Error)] Error
}

[JsonConverter(typeof(JsonStringEnumConverter<McpNativePickerOperation>))]
public enum McpNativePickerOperation
{
    [JsonStringEnumMemberName(NativePickerOperations.Detect)] Detect,
    [JsonStringEnumMemberName(NativePickerOperations.SelectPath)] SelectPath,
    [JsonStringEnumMemberName(NativePickerOperations.Confirm)] Confirm,
    [JsonStringEnumMemberName(NativePickerOperations.Cancel)] Cancel,
    [JsonStringEnumMemberName(NativePickerOperations.PredefineResult)] PredefineResult,
    [JsonStringEnumMemberName(NativePickerOperations.ConsumePredefinedResult)] ConsumePredefinedResult
}

[JsonConverter(typeof(JsonStringEnumConverter<McpNativePickerResult>))]
public enum McpNativePickerResult
{
    [JsonStringEnumMemberName(NativePickerResultStates.Success)] Success,
    [JsonStringEnumMemberName(NativePickerResultStates.Cancelled)] Cancelled,
    [JsonStringEnumMemberName(NativePickerResultStates.UnavailablePath)] UnavailablePath,
    [JsonStringEnumMemberName(NativePickerResultStates.DeletedPath)] DeletedPath
}

internal static class McpClosedValueNames
{
    public static string ToProtocolName(this McpInputAction value) => InputActions.All[(int)value];

    public static string ToProtocolName(this McpDiagnosticsMode value) => value switch
    {
        McpDiagnosticsMode.All => "all",
        McpDiagnosticsMode.ActiveOnly => "active-only",
        McpDiagnosticsMode.Minimal => "minimal",
        McpDiagnosticsMode.JsonMinimal => "json-minimal",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToProtocolName(this McpMutationOperation value) => value switch
    {
        McpMutationOperation.NoOp => RuntimeMutationOperationKinds.NoOp,
        McpMutationOperation.SetProperty => RuntimeMutationOperationKinds.SetProperty,
        McpMutationOperation.AddClass => RuntimeMutationOperationKinds.AddClass,
        McpMutationOperation.RemoveClass => RuntimeMutationOperationKinds.RemoveClass,
        McpMutationOperation.ToggleClass => RuntimeMutationOperationKinds.ToggleClass,
        McpMutationOperation.SetResource => RuntimeMutationOperationKinds.SetResource,
        McpMutationOperation.RemoveResource => RuntimeMutationOperationKinds.RemoveResource,
        McpMutationOperation.ResetMutation => RuntimeMutationOperationKinds.ResetMutation,
        McpMutationOperation.ResetAll => RuntimeMutationOperationKinds.ResetAll,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToProtocolName(this McpNativePickerOperation value) => value switch
    {
        McpNativePickerOperation.Detect => NativePickerOperations.Detect,
        McpNativePickerOperation.SelectPath => NativePickerOperations.SelectPath,
        McpNativePickerOperation.Confirm => NativePickerOperations.Confirm,
        McpNativePickerOperation.Cancel => NativePickerOperations.Cancel,
        McpNativePickerOperation.PredefineResult => NativePickerOperations.PredefineResult,
        McpNativePickerOperation.ConsumePredefinedResult => NativePickerOperations.ConsumePredefinedResult,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string ToProtocolName(this McpNativePickerResult value) => value switch
    {
        McpNativePickerResult.Success => NativePickerResultStates.Success,
        McpNativePickerResult.Cancelled => NativePickerResultStates.Cancelled,
        McpNativePickerResult.UnavailablePath => NativePickerResultStates.UnavailablePath,
        McpNativePickerResult.DeletedPath => NativePickerResultStates.DeletedPath,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
