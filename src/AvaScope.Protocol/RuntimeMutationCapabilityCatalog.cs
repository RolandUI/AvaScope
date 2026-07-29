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
                    ["scope"] = "local_session",
                    ["transport"] = "local_only",
                    ["temporary"] = "true",
                    ["maxDiagnostics"] = RuntimeMutationResponse.MaximumDiagnostics.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)
                }),
            new RuntimeMutationCapability(
                StyleLayoutMutation,
                available: styleLayoutAvailable,
                supportedOperations:
                RuntimeMutationOperationKinds.All.Where(static operation =>
                    operation != RuntimeMutationOperationKinds.NoOp).ToArray(),
                supportedProperties: RuntimeMutationPropertyNames.All,
                reason: styleLayoutAvailable
                    ? null
                    : "Style, layout, class, and resource mutation application is not available without an active local bridge.",
                metadata: new Dictionary<string, string>
                {
                    ["scope"] = "local_session",
                    ["transport"] = "local_only",
                    ["temporary"] = "true",
                    ["reversible"] = styleLayoutAvailable ? "true" : "false",
                    ["resetOperations"] = "reset_mutation,reset_all",
                    ["closeCleanup"] = "reset_active_mutations"
                })
        ];
    }
}
