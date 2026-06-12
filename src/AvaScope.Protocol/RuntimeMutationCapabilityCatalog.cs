namespace AvaScope.Protocol;

public static class RuntimeMutationCapabilityCatalog
{
    public const string RuntimeMutationContract = "runtime_mutation_contract";
    public const string StyleLayoutMutation = "style_layout_mutation";

    public static IReadOnlyList<RuntimeMutationCapability> ContractOnly()
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
                available: false,
                supportedOperations:
                [
                    RuntimeMutationOperationKinds.SetProperty,
                    RuntimeMutationOperationKinds.AddClass,
                    RuntimeMutationOperationKinds.RemoveClass,
                    RuntimeMutationOperationKinds.ToggleClass,
                    RuntimeMutationOperationKinds.SetResource,
                    RuntimeMutationOperationKinds.RemoveResource
                ],
                reason: "Style, layout, class, and resource mutation application is not enabled in this contract slice.")
        ];
    }
}
