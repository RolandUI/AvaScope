namespace AvaScope.Protocol;

public static class RuntimeMutationCapabilityCatalog
{
    public const string RuntimeMutationContract = "runtime_mutation_contract";
    public const string StyleLayoutMutation = "style_layout_mutation";

    public static IReadOnlyList<RuntimeMutationCapability> ContractOnly()
    {
        return CreateCapabilities(styleLayoutAvailable: false);
    }

    public static IReadOnlyList<RuntimeMutationCapability> CurrentBridgeCapabilities()
    {
        return CreateCapabilities(styleLayoutAvailable: true);
    }

    private static IReadOnlyList<RuntimeMutationCapability> CreateCapabilities(bool styleLayoutAvailable)
    {
        return
        [
            new RuntimeMutationCapability(
                RuntimeMutationContract,
                available: true,
                supportedOperations: [RuntimeMutationOperationKinds.NoOp],
                metadata: new Dictionary<string, string>
                {
                    ["maxDiagnostics"] = RuntimeMutationResponse.MaximumDiagnostics.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)
                }),
            new RuntimeMutationCapability(
                StyleLayoutMutation,
                available: styleLayoutAvailable,
                supportedOperations:
                [
                    RuntimeMutationOperationKinds.SetProperty,
                    RuntimeMutationOperationKinds.AddClass,
                    RuntimeMutationOperationKinds.RemoveClass,
                    RuntimeMutationOperationKinds.ToggleClass,
                    RuntimeMutationOperationKinds.SetResource,
                    RuntimeMutationOperationKinds.RemoveResource,
                    RuntimeMutationOperationKinds.ResetMutation,
                    RuntimeMutationOperationKinds.ResetAll
                ],
                reason: styleLayoutAvailable
                    ? null
                    : "Style, layout, class, and resource mutation application is not available without an active local bridge.")
        ];
    }
}
