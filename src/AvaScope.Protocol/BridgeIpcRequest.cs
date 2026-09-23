using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record BridgeIpcRequest
{
    [JsonConstructor]
    public BridgeIpcRequest(
        string requestId,
        string method,
        string? topLevelId = null,
        string? outputPath = null,
        int? maxDepth = null,
        string? treeKind = null,
        string? nodeType = null,
        string? name = null,
        string? automationId = null,
        string? text = null,
        int? maxResults = null,
        string? nodeId = null,
        string? action = null,
        double? x = null,
        double? y = null,
        string? inputText = null,
        string? targetNodeId = null,
        string? inputKey = null,
        string? keyModifiers = null,
        double? deltaX = null,
        double? deltaY = null,
        RuntimeMutationRequest? mutation = null,
        bool includeChildren = false,
        bool includeBounds = true,
        bool includeAccessibility = false,
        bool includeBindings = false,
        int? maxResponseDepth = null,
        InputGestureOptions? gesture = null,
        RuntimeTargetContext? customActionTarget = null,
        RuntimeCustomActionRequest? customAction = null,
        bool? visible = null,
        bool? enabled = null,
        bool? rendered = null,
        bool? actionable = null,
        RuntimeTargetContext? inputTarget = null,
        RuntimeTargetContext? gestureDestinationTarget = null,
        RuntimeReadinessProbeOptions? readiness = null,
        RuntimeObservationRequest? observation = null,
        RuntimeObservationChangesRequest? observationChanges = null,
        RuntimeVirtualItemRequest? virtualItem = null,
        InputExecutionOptions? inputExecution = null,
        RuntimeNativePickerRequest? nativePicker = null,
        SessionControlRequest? sessionControl = null,
        string? controlToken = null,
        RuntimeActionExplanationRequest? actionExplanation = null,
        RuntimeQueryRequest? query = null,
        RuntimeDesiredStateRequest? desiredState = null,
        RuntimeFormInspectionRequest? formInspection = null,
        RuntimeFormFillRequest? formFill = null,
        RuntimeTableQueryRequest? tableQuery = null,
        RuntimeTableActionRequest? tableAction = null,
        RuntimeActionMapRequest? actionMap = null,
        RuntimeFocusInspectionRequest? focusInspection = null,
        RuntimeFocusProbeRequest? focusProbe = null,
        RuntimeExpressionRequest? expression = null,
        RuntimeOperationRequest? operation = null,
        RuntimeTraceRequest? trace = null,
        RuntimeTextEditRequest? textEdit = null,
        RuntimeSceneRequest? scene = null,
        RuntimeNavigationRequest? navigation = null,
        RuntimeWindowRequest? window = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Request id cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Method cannot be empty.", nameof(method));
        }

        RequestId = requestId;
        ActionMap = actionMap;
        FocusInspection = focusInspection;
        FocusProbe = focusProbe;
        Expression = expression;
        Operation = operation;
        Trace = trace;
        TextEdit = textEdit;
        Scene = scene;
        Navigation = navigation;
        Window = window;
        Method = method;
        TopLevelId = topLevelId;
        OutputPath = outputPath;
        MaxDepth = maxDepth;
        TreeKind = treeKind;
        NodeType = nodeType;
        Name = name;
        AutomationId = automationId;
        Text = text;
        MaxResults = maxResults;
        NodeId = nodeId;
        Action = action;
        X = x;
        Y = y;
        InputText = inputText;
        TargetNodeId = targetNodeId;
        InputKey = inputKey;
        KeyModifiers = keyModifiers;
        DeltaX = deltaX;
        DeltaY = deltaY;
        Mutation = mutation;
        IncludeChildren = includeChildren;
        IncludeBounds = includeBounds;
        IncludeAccessibility = includeAccessibility;
        IncludeBindings = includeBindings;
        MaxResponseDepth = maxResponseDepth;
        Gesture = gesture;
        CustomActionTarget = customActionTarget;
        CustomAction = customAction;
        Visible = visible;
        Enabled = enabled;
        Rendered = rendered;
        Actionable = actionable;
        InputTarget = inputTarget;
        GestureDestinationTarget = gestureDestinationTarget;
        Readiness = readiness;
        Observation = observation;
        ObservationChanges = observationChanges;
        VirtualItem = virtualItem;
        InputExecution = inputExecution;
        NativePicker = nativePicker;
        SessionControl = sessionControl;
        ControlToken = controlToken;
        ActionExplanation = actionExplanation;
        DesiredState = desiredState;
        FormInspection = formInspection;
        FormFill = formFill;
        TableQuery = tableQuery;
        TableAction = tableAction;
        Query = query;
    }

    [JsonPropertyName("actionMap")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeActionMapRequest? ActionMap { get; }

    [JsonPropertyName("focusInspection"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeFocusInspectionRequest? FocusInspection { get; }
    [JsonPropertyName("focusProbe"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeFocusProbeRequest? FocusProbe { get; }
    [JsonPropertyName("expression"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeExpressionRequest? Expression { get; }
    [JsonPropertyName("operation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeOperationRequest? Operation { get; }
    [JsonPropertyName("trace"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTraceRequest? Trace { get; }

    [JsonPropertyName("textEdit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTextEditRequest? TextEdit { get; }

    [JsonPropertyName("scene"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeSceneRequest? Scene { get; }

    [JsonPropertyName("navigation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeNavigationRequest? Navigation { get; }

    [JsonPropertyName("window"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeWindowRequest? Window { get; }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("actionExplanation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeActionExplanationRequest? ActionExplanation { get; }

    [JsonPropertyName("query"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeQueryRequest? Query { get; }

    [JsonPropertyName("desiredState"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeDesiredStateRequest? DesiredState { get; }

    [JsonPropertyName("formInspection"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeFormInspectionRequest? FormInspection { get; }

    [JsonPropertyName("formFill"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeFormFillRequest? FormFill { get; }

    [JsonPropertyName("tableQuery"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTableQueryRequest? TableQuery { get; }

    [JsonPropertyName("tableAction"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTableActionRequest? TableAction { get; }

    [JsonPropertyName("inputExecution")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InputExecutionOptions? InputExecution { get; }

    [JsonPropertyName("nativePicker")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeNativePickerRequest? NativePicker { get; }

    [JsonPropertyName("sessionControl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SessionControlRequest? SessionControl { get; }

    [JsonPropertyName("controlToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ControlToken { get; init; }

    [JsonPropertyName("readiness")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeReadinessProbeOptions? Readiness { get; }

    [JsonPropertyName("observation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeObservationRequest? Observation { get; }

    [JsonPropertyName("observationChanges")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeObservationChangesRequest? ObservationChanges { get; }

    [JsonPropertyName("virtualItem")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeVirtualItemRequest? VirtualItem { get; }

    [JsonPropertyName("method")]
    public string Method { get; }

    [JsonPropertyName("topLevelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelId { get; }

    [JsonPropertyName("outputPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputPath { get; }

    [JsonPropertyName("maxDepth")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxDepth { get; }

    [JsonPropertyName("treeKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TreeKind { get; }

    [JsonPropertyName("nodeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeType { get; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; }

    [JsonPropertyName("automationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationId { get; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; }

    [JsonPropertyName("maxResults")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxResults { get; }

    [JsonPropertyName("nodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeId { get; }

    [JsonPropertyName("action")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Action { get; }

    [JsonPropertyName("x")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? X { get; }

    [JsonPropertyName("y")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Y { get; }

    [JsonPropertyName("inputText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputText { get; }

    [JsonPropertyName("targetNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetNodeId { get; }

    [JsonPropertyName("inputKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputKey { get; }

    [JsonPropertyName("keyModifiers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KeyModifiers { get; }

    [JsonPropertyName("deltaX")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? DeltaX { get; }

    [JsonPropertyName("deltaY")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? DeltaY { get; }

    [JsonPropertyName("mutation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeMutationRequest? Mutation { get; }

    [JsonPropertyName("includeChildren")]
    public bool IncludeChildren { get; }

    [JsonPropertyName("includeBounds")]
    public bool IncludeBounds { get; }

    [JsonPropertyName("includeAccessibility")]
    public bool IncludeAccessibility { get; }

    [JsonPropertyName("includeBindings")]
    public bool IncludeBindings { get; }

    [JsonPropertyName("maxResponseDepth")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxResponseDepth { get; }

    [JsonPropertyName("gesture")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InputGestureOptions? Gesture { get; }

    [JsonPropertyName("customActionTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? CustomActionTarget { get; }

    [JsonPropertyName("customAction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeCustomActionRequest? CustomAction { get; }

    [JsonPropertyName("visible")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Visible { get; }

    [JsonPropertyName("enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; }

    [JsonPropertyName("rendered")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Rendered { get; }

    [JsonPropertyName("actionable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Actionable { get; }

    [JsonPropertyName("inputTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? InputTarget { get; }

    [JsonPropertyName("gestureDestinationTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? GestureDestinationTarget { get; }
}
