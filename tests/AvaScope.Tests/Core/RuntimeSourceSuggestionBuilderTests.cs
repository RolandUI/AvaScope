using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class RuntimeSourceSuggestionBuilderTests
{
    [Fact]
    public void CreateSuggestionsUsesSourceContextForPropertyMutation()
    {
        var sessionId = new SessionId("session-1");
        var target = new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual, "visual:button");
        var entry = new RuntimeMutationReviewEntry(
            1,
            "request-1",
            "mutation:1",
            sessionId,
            target.TopLevelId,
            target,
            new RuntimeMutationOperation(
                RuntimeMutationOperationKinds.SetProperty,
                propertyName: "Width",
                value: "240",
                valueType: "double"),
            RuntimeMutationStatuses.Applied,
            applied: true,
            active: true,
            DateTimeOffset.UnixEpoch,
            metadata: new Dictionary<string, string>
            {
                ["propertyName"] = "Width",
                ["nodeType"] = "Avalonia.Controls.Button",
                ["effectiveValue"] = "240",
                ["effectiveValueSource"] = "local_value"
            });
        var response = CreateReview(sessionId, entry);
        var context = new RuntimeSourceSuggestionContext(
            "C:\\app\\Sample.csproj",
            "C:\\app\\Views\\MainView.axaml",
            "C:\\app\\App.axaml",
            source: "test");

        var updated = RuntimeSourceSuggestionBuilder.WithSourceContext(response, context);

        Assert.Equal(context, updated.SourceContext);
        var suggestion = Assert.Single(updated.SourceSuggestions);
        Assert.Equal(entry.MutationId, suggestion.MutationId);
        Assert.Equal("medium", suggestion.Confidence);
        Assert.Equal("runtime_mutation_metadata+source_context", suggestion.Provenance);
        Assert.Equal("xaml_property_or_style_setter", suggestion.SuggestedTargetKind);
        Assert.Equal("provided", suggestion.SourceFileStatus);
        Assert.Equal(Path.GetFullPath("C:\\app\\Views\\MainView.axaml"), suggestion.SuggestedFilePath);
        Assert.Equal("Width", suggestion.SuggestedProperty);
        Assert.Contains("temporary local overrides", suggestion.Limitations[0], StringComparison.Ordinal);
        Assert.Equal("local_value", suggestion.Metadata["effectiveValueSource"]);
    }

    [Fact]
    public void CreateSuggestionsReportsUnknownSourceFileWhenContextIsUnavailable()
    {
        var sessionId = new SessionId("session-1");
        var target = new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual, "visual:border");
        var entry = new RuntimeMutationReviewEntry(
            2,
            "request-2",
            "mutation:2",
            sessionId,
            target.TopLevelId,
            target,
            new RuntimeMutationOperation(
                RuntimeMutationOperationKinds.SetResource,
                resourceKey: "AccentBrush",
                value: "#336699",
                valueType: "brush"),
            RuntimeMutationStatuses.Applied,
            applied: true,
            active: true,
            DateTimeOffset.UnixEpoch,
            metadata: new Dictionary<string, string>
            {
                ["resourceKey"] = "AccentBrush",
                ["effectiveValueSource"] = "local_resource"
            });
        var response = CreateReview(sessionId, entry);

        var updated = RuntimeSourceSuggestionBuilder.WithSourceContext(response, sourceContext: null);

        Assert.Null(updated.SourceContext);
        var suggestion = Assert.Single(updated.SourceSuggestions);
        Assert.Equal("unknown", suggestion.Confidence);
        Assert.Equal("runtime_mutation_metadata", suggestion.Provenance);
        Assert.Equal("resource", suggestion.SuggestedTargetKind);
        Assert.Equal("unknown", suggestion.SourceFileStatus);
        Assert.Null(suggestion.SuggestedFilePath);
        Assert.Equal("AccentBrush", suggestion.SuggestedResourceKey);
        Assert.Contains(
            suggestion.Limitations,
            limitation => limitation.Contains("No stable source file mapping", StringComparison.Ordinal));
    }

    private static RuntimeMutationReviewResponse CreateReview(
        SessionId sessionId,
        RuntimeMutationReviewEntry entry)
    {
        return new RuntimeMutationReviewResponse(
            sessionId,
            DateTimeOffset.UnixEpoch,
            historyCount: 1,
            activeMutationCount: 1,
            history: [entry],
            activeMutations: [entry],
            resetHandoff: new RuntimeMutationResetHandoff(
                sessionId,
                activeMutationCount: 1,
                activeMutationIds: [entry.MutationId],
                suggestedResetAllTarget: entry.Target));
    }
}
