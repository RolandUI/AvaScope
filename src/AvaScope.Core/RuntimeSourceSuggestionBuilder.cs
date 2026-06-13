using System.Globalization;
using AvaScope.Protocol;

namespace AvaScope.Core;

public static class RuntimeSourceSuggestionBuilder
{
    public const int MaximumSuggestions = 32;

    public static RuntimeMutationReviewResponse WithSourceContext(
        RuntimeMutationReviewResponse response,
        RuntimeSourceSuggestionContext? sourceContext)
    {
        ArgumentNullException.ThrowIfNull(response);

        var suggestions = CreateSuggestions(response, sourceContext);
        return new RuntimeMutationReviewResponse(
            response.SessionId,
            response.ReviewedAt,
            response.HistoryCount,
            response.ActiveMutationCount,
            response.History,
            response.ActiveMutations,
            response.ResetHandoff,
            response.Metadata,
            response.ReviewArtifact,
            sourceContext,
            suggestions);
    }

    public static IReadOnlyList<RuntimeSourceSuggestion> CreateSuggestions(
        RuntimeMutationReviewResponse response,
        RuntimeSourceSuggestionContext? sourceContext = null)
    {
        ArgumentNullException.ThrowIfNull(response);

        return SelectEntries(response)
            .Select((entry, index) => CreateSuggestion(entry, sourceContext, index + 1))
            .Where(static suggestion => suggestion is not null)
            .Cast<RuntimeSourceSuggestion>()
            .Take(MaximumSuggestions)
            .ToArray();
    }

    private static IEnumerable<RuntimeMutationReviewEntry> SelectEntries(RuntimeMutationReviewResponse response)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in response.ActiveMutations.Concat(response.History))
        {
            if (!entry.Applied || !seen.Add(entry.MutationId))
            {
                continue;
            }

            yield return entry;
        }
    }

    private static RuntimeSourceSuggestion? CreateSuggestion(
        RuntimeMutationReviewEntry entry,
        RuntimeSourceSuggestionContext? sourceContext,
        int suggestionIndex)
    {
        var kind = entry.Operation.Kind;
        if (kind is RuntimeMutationOperationKinds.ResetMutation
            or RuntimeMutationOperationKinds.ResetAll
            or RuntimeMutationOperationKinds.NoOp)
        {
            return null;
        }

        var targetKind = SuggestedTargetKind(kind);
        var suggestedProperty = FirstNonEmpty(entry.Operation.PropertyName, GetMetadata(entry, "propertyName"));
        var suggestedClass = FirstNonEmpty(entry.Operation.ClassName, GetMetadata(entry, "className"));
        var suggestedResourceKey = FirstNonEmpty(entry.Operation.ResourceKey, GetMetadata(entry, "resourceKey"));
        var suggestedMember = kind switch
        {
            RuntimeMutationOperationKinds.SetProperty => suggestedProperty,
            RuntimeMutationOperationKinds.AddClass
                or RuntimeMutationOperationKinds.RemoveClass
                or RuntimeMutationOperationKinds.ToggleClass => suggestedClass,
            RuntimeMutationOperationKinds.SetResource
                or RuntimeMutationOperationKinds.RemoveResource => suggestedResourceKey,
            _ => null
        };
        var filePath = SelectSuggestedFilePath(kind, sourceContext);
        var sourceFileStatus = filePath is null ? "unknown" : "provided";
        var confidence = ConfidenceFor(sourceContext, filePath);
        var provenance = sourceContext?.HasAnyPath == true
            ? "runtime_mutation_metadata+source_context"
            : "runtime_mutation_metadata";
        var limitations = CreateLimitations(kind, filePath).ToArray();
        var metadata = CreateMetadata(entry, sourceContext);

        return new RuntimeSourceSuggestion(
            $"source-suggestion:{entry.MutationId}:{suggestionIndex.ToString(CultureInfo.InvariantCulture)}",
            entry.MutationId,
            entry.Sequence,
            kind,
            entry.Target,
            confidence,
            provenance,
            targetKind,
            sourceFileStatus,
            CreateSuggestedAction(kind, entry, suggestedMember, filePath),
            filePath,
            suggestedMember,
            suggestedProperty,
            suggestedClass,
            suggestedResourceKey,
            limitations,
            metadata);
    }

    private static string SuggestedTargetKind(string operationKind)
    {
        return operationKind switch
        {
            RuntimeMutationOperationKinds.SetProperty => "xaml_property_or_style_setter",
            RuntimeMutationOperationKinds.AddClass
                or RuntimeMutationOperationKinds.RemoveClass
                or RuntimeMutationOperationKinds.ToggleClass => "style_class",
            RuntimeMutationOperationKinds.SetResource
                or RuntimeMutationOperationKinds.RemoveResource => "resource",
            _ => "unknown"
        };
    }

    private static string? SelectSuggestedFilePath(string operationKind, RuntimeSourceSuggestionContext? sourceContext)
    {
        if (sourceContext is null)
        {
            return null;
        }

        return operationKind switch
        {
            RuntimeMutationOperationKinds.SetResource
                or RuntimeMutationOperationKinds.RemoveResource => sourceContext.AppXamlPath
                    ?? sourceContext.ViewPath
                    ?? sourceContext.ProjectPath,
            _ => sourceContext.ViewPath
                ?? sourceContext.AppXamlPath
                ?? sourceContext.ProfileFilePath
                ?? sourceContext.ProjectPath
        };
    }

    private static string ConfidenceFor(RuntimeSourceSuggestionContext? sourceContext, string? filePath)
    {
        if (filePath is null)
        {
            return "unknown";
        }

        if (string.Equals(filePath, sourceContext?.ViewPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(filePath, sourceContext?.AppXamlPath, StringComparison.OrdinalIgnoreCase))
        {
            return "medium";
        }

        return "low";
    }

    private static string CreateSuggestedAction(
        string operationKind,
        RuntimeMutationReviewEntry entry,
        string? suggestedMember,
        string? filePath)
    {
        var target = entry.Target.NodeId ?? entry.Target.TopLevelId;
        var fileHint = filePath is null
            ? "Provide source context or inspect the project manually before editing."
            : $"Review '{filePath}' before making a manual source change.";

        return operationKind switch
        {
            RuntimeMutationOperationKinds.SetProperty =>
                $"Consider applying '{suggestedMember ?? "the mutated property"}' for target '{target}' in the owning XAML element, style setter, or template. {fileHint}",
            RuntimeMutationOperationKinds.AddClass
                or RuntimeMutationOperationKinds.RemoveClass
                or RuntimeMutationOperationKinds.ToggleClass =>
                $"Search for class '{suggestedMember ?? "the mutated class"}' selectors and target '{target}' before deciding whether the class belongs on the element or in a style. {fileHint}",
            RuntimeMutationOperationKinds.SetResource
                or RuntimeMutationOperationKinds.RemoveResource =>
                $"Inspect resource key '{suggestedMember ?? "the mutated resource"}' and its lookup scope before moving the runtime override into source. {fileHint}",
            _ =>
                $"Review mutation '{entry.MutationId}' manually. {fileHint}"
        };
    }

    private static IEnumerable<string> CreateLimitations(string operationKind, string? filePath)
    {
        yield return "Runtime mutations are temporary local overrides; this suggestion is advisory and does not modify source files.";

        if (filePath is null)
        {
            yield return "No stable source file mapping was available from the bridge session; pass source context to improve the handoff.";
        }

        if (operationKind is RuntimeMutationOperationKinds.AddClass
            or RuntimeMutationOperationKinds.RemoveClass
            or RuntimeMutationOperationKinds.ToggleClass)
        {
            yield return "Classes can be applied by styles or templates; search selectors and template boundaries before editing the view.";
        }

        if (operationKind is RuntimeMutationOperationKinds.SetResource
            or RuntimeMutationOperationKinds.RemoveResource)
        {
            yield return "Resources can resolve from local, application, theme, or included dictionaries; verify the owning scope before editing.";
        }

        if (operationKind == RuntimeMutationOperationKinds.SetProperty)
        {
            yield return "Property values can come from local values, styles, templates, bindings, or animations; verify the owning source before patching.";
        }
    }

    private static IReadOnlyDictionary<string, string> CreateMetadata(
        RuntimeMutationReviewEntry entry,
        RuntimeSourceSuggestionContext? sourceContext)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nodeType"] = GetMetadata(entry, "nodeType") ?? "unknown",
            ["target"] = entry.Target.NodeId ?? entry.Target.TopLevelId,
            ["sourceContext"] = sourceContext?.Source ?? "unknown"
        };

        CopyMetadata(entry, metadata, "propertyName");
        CopyMetadata(entry, metadata, "className");
        CopyMetadata(entry, metadata, "resourceKey");
        CopyMetadata(entry, metadata, "originalValueSource");
        CopyMetadata(entry, metadata, "effectiveValueSource");
        CopyMetadata(entry, metadata, "effectiveValue");

        return metadata;
    }

    private static void CopyMetadata(RuntimeMutationReviewEntry entry, IDictionary<string, string> target, string key)
    {
        var value = GetMetadata(entry, key);
        if (value is not null)
        {
            target[key] = value;
        }
    }

    private static string? GetMetadata(RuntimeMutationReviewEntry entry, string key)
    {
        return entry.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
