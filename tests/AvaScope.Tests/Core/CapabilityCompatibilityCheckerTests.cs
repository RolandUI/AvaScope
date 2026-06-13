using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class CapabilityCompatibilityCheckerTests
{
    [Fact]
    public void CreateResponseReturnsCurrentCapabilitiesWhenRequirementsAreSupported()
    {
        var result = new CapabilityCompatibilityChecker().CreateResponse(
            $"{AvaScopeCapabilityIds.ProtocolCapabilityDiscovery},{AvaScopeCapabilityIds.RuntimeUiAudit}");

        Assert.True(result.Success, result.Error?.Message);
        Assert.Contains(result.Value!.Capabilities, capability =>
            capability.Id == AvaScopeCapabilityIds.ProtocolCapabilityDiscovery
            && capability.Status == AvaScopeCapabilityStatuses.Available);
        Assert.Contains(result.Value.Tools, tool =>
            tool.Adapter == "mcp"
            && tool.Name == "capabilities"
            && tool.CapabilityIds.Contains(AvaScopeCapabilityIds.ProtocolCapabilityDiscovery));
    }

    [Fact]
    public void CreateResponseFailsWithActionableDiagnosticForUnsupportedRequirement()
    {
        var result = new CapabilityCompatibilityChecker().CreateResponse("post_1_0.magic");

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(AvaScopeCapabilityErrorCodes.CapabilityNotSupported, result.Error!.Code);
        Assert.Equal("post_1_0.magic", result.Error.Details!["unsupportedCapabilities"]);
        Assert.Equal(AvaScopeProtocol.CurrentVersion.ToString(), result.Error.Details["protocolVersion"]);
        Assert.Contains("capabilities", result.Error.Details["nextAction"], StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateFailsWhenOlderCapabilityManifestDoesNotAdvertiseRequiredFeature()
    {
        var current = AvaScopeCapabilityCatalog.Current();
        var olderManifest = new AvaScopeCapabilitiesResponse(
            current.ServiceName,
            current.ProtocolVersion,
            current.GeneratedAt,
            current.CompatibilityPolicy,
            current.Capabilities
                .Where(static capability => capability.Id != AvaScopeCapabilityIds.RuntimeSourceSuggestions)
                .ToArray(),
            current.Tools,
            RuntimeMutationCapabilityCatalog.ContractOnly());

        var result = new CapabilityCompatibilityChecker().Validate(
            olderManifest,
            [AvaScopeCapabilityIds.RuntimeSourceSuggestions]);

        Assert.False(result.Success);
        Assert.Equal(AvaScopeCapabilityErrorCodes.CapabilityNotSupported, result.Error!.Code);
        Assert.Equal(AvaScopeCapabilityIds.RuntimeSourceSuggestions, result.Error.Details!["unsupportedCapabilities"]);
    }

    [Fact]
    public void ParseRequiredCapabilitiesAcceptsCommaSemicolonAndSpaceSeparators()
    {
        var parsed = CapabilityCompatibilityChecker.ParseRequiredCapabilities(
            $" {AvaScopeCapabilityIds.PreviewAxaml},{AvaScopeCapabilityIds.PreviewAxaml};{AvaScopeCapabilityIds.ReportsJson} {AvaScopeCapabilityIds.ArtifactsHtmlViewer}");

        Assert.Equal(
            [
                AvaScopeCapabilityIds.PreviewAxaml,
                AvaScopeCapabilityIds.ReportsJson,
                AvaScopeCapabilityIds.ArtifactsHtmlViewer
            ],
            parsed);
    }
}
