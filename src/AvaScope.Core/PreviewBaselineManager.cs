using System.Globalization;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewBaselineManager
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly PreviewHostClient _previewHostClient;
    private readonly PreviewImageDiffer _imageDiffer;
    private readonly TimeProvider _timeProvider;

    public PreviewBaselineManager(PreviewHostClient previewHostClient)
        : this(previewHostClient, new PreviewImageDiffer(), TimeProvider.System)
    {
    }

    public PreviewBaselineManager(
        PreviewHostClient previewHostClient,
        PreviewImageDiffer imageDiffer,
        TimeProvider timeProvider)
    {
        _previewHostClient = previewHostClient ?? throw new ArgumentNullException(nameof(previewHostClient));
        _imageDiffer = imageDiffer ?? throw new ArgumentNullException(nameof(imageDiffer));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<CoreResult<PreviewBaselineCreateResponse>> CreateAsync(
        PreviewRequest request,
        IReadOnlyList<PreviewViewport> viewports,
        string manifestPath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(viewports);

        if (viewports.Count == 0)
        {
            return CoreResult<PreviewBaselineCreateResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidPreviewRequest,
                "At least one baseline viewport size is required."));
        }

        var fullManifestPath = Path.GetFullPath(manifestPath);
        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        var baseOutputPath = Path.Combine(fullOutputDirectory, "baseline.png");
        var baselineRequest = CreateRequestForOutput(request, baseOutputPath, width: null, height: null);
        var render = await _previewHostClient.RenderBatchAsync(
            baselineRequest,
            viewports,
            cancellationToken: cancellationToken);
        if (!render.Success)
        {
            return CoreResult<PreviewBaselineCreateResponse>.Fail(render.Error!);
        }

        var failedEntry = render.Value!.Entries.FirstOrDefault(static entry => !entry.Render.Success);
        if (failedEntry is not null)
        {
            return CoreResult<PreviewBaselineCreateResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewBaselineFailed,
                $"Baseline render failed for viewport {failedEntry.Viewport.Width.ToString(CultureInfo.InvariantCulture)}x{failedEntry.Viewport.Height.ToString(CultureInfo.InvariantCulture)}.",
                failedEntry.Render.Error?.Details));
        }

        var entries = render.Value.Entries
            .Select((entry, index) => new PreviewBaselineEntry(
                index,
                entry.Viewport,
                entry.Render.Value!.FilePath,
                request.Dpi,
                request.ProjectPath,
                request.ViewPath,
                request.ThemeVariant,
                request.Culture,
                request.DesignDataType))
            .ToArray();
        var manifest = new PreviewBaselineManifest(
            PreviewBaselineManifest.CurrentVersion,
            _timeProvider.GetUtcNow(),
            entries);

        var written = WriteBaselineManifest(manifest, fullManifestPath);
        if (!written.Success)
        {
            return CoreResult<PreviewBaselineCreateResponse>.Fail(written.Error!);
        }

        return CoreResult<PreviewBaselineCreateResponse>.Ok(new PreviewBaselineCreateResponse(
            fullManifestPath,
            manifest,
            render.Value));
    }

    public async Task<CoreResult<PreviewBaselineCreateResponse>> CreateSuiteAsync(
        string suiteManifestPath,
        string baselineManifestPath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var fullBaselineManifestPath = Path.GetFullPath(baselineManifestPath);
        var expansionResult = ExpandSuiteManifest(suiteManifestPath, outputDirectory);
        if (!expansionResult.Success)
        {
            return CoreResult<PreviewBaselineCreateResponse>.Fail(expansionResult.Error!);
        }

        var expansions = expansionResult.Value!;
        var renderEntries = new List<PreviewBatchEntry>(expansions.Count);
        var baselineEntries = new List<PreviewBaselineEntry>(expansions.Count);
        foreach (var expansion in expansions)
        {
            var request = new PreviewRequest(
                expansion.ImagePath,
                expansion.Viewport.Width,
                expansion.Viewport.Height,
                expansion.Dpi,
                expansion.ProjectPath,
                expansion.ViewPath,
                expansion.ThemeVariant,
                expansion.Culture,
                expansion.DesignDataType,
                expansion.AnimationTimeOffsetMs);
            var render = await _previewHostClient.RenderAsync(request, cancellationToken);
            var renderResult = ToToolResult(render);
            renderEntries.Add(new PreviewBatchEntry(
                expansion.Viewport,
                expansion.ImagePath,
                renderResult));

            if (!render.Success)
            {
                return CoreResult<PreviewBaselineCreateResponse>.Fail(new CoreError(
                    CoreErrorCodes.PreviewBaselineFailed,
                    $"Baseline suite render failed for '{expansion.EntryId}' variant '{expansion.VariantName}'.",
                    render.Error?.Details));
            }

            baselineEntries.Add(new PreviewBaselineEntry(
                expansion.Index,
                expansion.Viewport,
                render.Value!.FilePath,
                expansion.Dpi,
                expansion.ProjectPath,
                expansion.ViewPath,
                expansion.ThemeVariant,
                expansion.Culture,
                expansion.DesignDataType,
                expansion.SuiteName,
                expansion.EntryId,
                expansion.VariantName,
                expansion.ProfileName,
                expansion.ProfileVariant,
                expansion.ProfileFilePath,
                expansion.RuntimeTarget,
                expansion.MutationPresetIds,
                expansion.AnimationTimeOffsetMs,
                expansion.ComparisonRules));
        }

        var manifest = new PreviewBaselineManifest(
            PreviewBaselineManifest.CurrentVersion,
            _timeProvider.GetUtcNow(),
            baselineEntries);
        var written = WriteBaselineManifest(manifest, fullBaselineManifestPath);
        if (!written.Success)
        {
            return CoreResult<PreviewBaselineCreateResponse>.Fail(written.Error!);
        }

        return CoreResult<PreviewBaselineCreateResponse>.Ok(new PreviewBaselineCreateResponse(
            fullBaselineManifestPath,
            manifest,
            new PreviewBatchResponse(
                renderEntries,
                null,
                _timeProvider.GetUtcNow())));
    }

    public CoreResult<IReadOnlyList<PreviewBaselineSuiteExpansion>> ExpandSuiteManifest(
        string suiteManifestPath,
        string outputDirectory)
    {
        var fullSuiteManifestPath = Path.GetFullPath(suiteManifestPath);
        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        var suiteResult = LoadSuiteManifest(fullSuiteManifestPath);
        if (!suiteResult.Success)
        {
            return CoreResult<IReadOnlyList<PreviewBaselineSuiteExpansion>>.Fail(suiteResult.Error!);
        }

        return ExpandSuiteManifest(suiteResult.Value!, fullSuiteManifestPath, fullOutputDirectory);
    }

    public async Task<CoreResult<PreviewBaselineCheckResponse>> CheckAsync(
        string manifestPath,
        string outputDirectory,
        string diffDirectory,
        double tolerance,
        string? reportPath = null,
        string? reportPackDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (tolerance < 0 || tolerance > 255)
        {
            return CoreResult<PreviewBaselineCheckResponse>.Fail(new CoreError(
                CoreErrorCodes.ImageDiffFailed,
                "Tolerance must be between 0 and 255."));
        }

        var fullManifestPath = Path.GetFullPath(manifestPath);
        var manifestResult = LoadManifest(fullManifestPath);
        if (!manifestResult.Success)
        {
            return CoreResult<PreviewBaselineCheckResponse>.Fail(manifestResult.Error!);
        }

        var manifest = manifestResult.Value!;
        var entries = new List<PreviewBaselineCheckEntry>(manifest.Entries.Count);
        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        var fullDiffDirectory = Path.GetFullPath(diffDirectory);
        var fullReportPath = string.IsNullOrWhiteSpace(reportPath) ? null : Path.GetFullPath(reportPath);
        var fullReportPackDirectory = string.IsNullOrWhiteSpace(reportPackDirectory)
            ? null
            : Path.GetFullPath(reportPackDirectory);
        Directory.CreateDirectory(fullOutputDirectory);
        Directory.CreateDirectory(fullDiffDirectory);

        var passed = true;
        foreach (var baseline in manifest.Entries)
        {
            var token = CreateVariantToken(baseline.Index, baseline.Viewport);
            var currentPath = Path.Combine(fullOutputDirectory, $"current-{token}.png");
            var diffPath = Path.Combine(fullDiffDirectory, $"diff-{token}.png");
            var comparisonRules = baseline.ComparisonRules;
            var effectiveTolerance = comparisonRules?.Tolerance ?? tolerance;
            var request = new PreviewRequest(
                currentPath,
                baseline.Viewport.Width,
                baseline.Viewport.Height,
                baseline.Dpi,
                baseline.ProjectPath,
                baseline.ViewPath,
                baseline.ThemeVariant,
                baseline.Culture,
                baseline.DesignDataType,
                baseline.AnimationTimeOffsetMs);
            var render = await _previewHostClient.RenderAsync(request, cancellationToken);
            var renderResult = ToToolResult(render);
            ToolResult<PreviewDiffResponse> diffResult;
            IReadOnlyList<PreviewBaselineRegionCheckResult> requiredRegionResults = [];
            if (render.Success)
            {
                var diff = _imageDiffer.Compare(
                    baseline.ImagePath,
                    render.Value!.FilePath,
                    diffPath,
                    effectiveTolerance,
                    comparisonRules?.IgnoredRegions,
                    comparisonRules?.MaxChangedPixels,
                    comparisonRules?.MaxChangedPercent);
                diffResult = ToToolResult(diff);
                if (!diff.Success || !diff.Value!.Passed)
                {
                    passed = false;
                }

                if (diff.Success && comparisonRules?.RequiredRegions.Count > 0)
                {
                    requiredRegionResults = EvaluateRequiredRegions(
                        baseline,
                        currentPath,
                        fullDiffDirectory,
                        token,
                        effectiveTolerance,
                        comparisonRules.RequiredRegions);
                    if (requiredRegionResults.Any(static result => !result.Result.Success || !result.Result.Value!.Passed))
                    {
                        passed = false;
                    }
                }
            }
            else
            {
                passed = false;
                diffResult = ToolResult<PreviewDiffResponse>.Fail(new ProtocolError(
                    render.Error!.Code,
                    render.Error.Message,
                    render.Error.Details));
            }

            entries.Add(new PreviewBaselineCheckEntry(
                baseline,
                currentPath,
                diffPath,
                renderResult,
                diffResult,
                comparisonRules,
                requiredRegionResults));
        }

        var checkedAt = _timeProvider.GetUtcNow();
        var response = new PreviewBaselineCheckResponse(
            fullManifestPath,
            passed,
            entries,
            checkedAt,
            fullReportPath);
        if (fullReportPackDirectory is not null)
        {
            var exported = new PreviewBaselineReportPackExporter(_timeProvider).Export(
                response,
                fullReportPackDirectory);
            if (!exported.Success)
            {
                return CoreResult<PreviewBaselineCheckResponse>.Fail(exported.Error!);
            }
            response = new PreviewBaselineCheckResponse(
                fullManifestPath,
                passed,
                entries,
                checkedAt,
                fullReportPath,
                exported.Value!);
        }

        if (fullReportPath is not null)
        {
            var written = WriteBaselineCheckReport(response, fullReportPath);
            if (!written.Success)
            {
                return CoreResult<PreviewBaselineCheckResponse>.Fail(written.Error!);
            }
        }

        return CoreResult<PreviewBaselineCheckResponse>.Ok(response);
    }

    private static IReadOnlyList<PreviewBaselineRegionCheckResult> EvaluateRequiredRegions(
        PreviewBaselineEntry baseline,
        string currentPath,
        string diffDirectory,
        string token,
        double tolerance,
        IReadOnlyList<PreviewRequiredRegion> requiredRegions)
    {
        var asserter = new ScreenshotRegionAsserter();
        var results = new List<PreviewBaselineRegionCheckResult>(requiredRegions.Count);
        for (var index = 0; index < requiredRegions.Count; index++)
        {
            var rule = requiredRegions[index];
            var cropToken = Slug(rule.Region.Name ?? rule.Assertion);
            var cropPath = Path.Combine(diffDirectory, $"required-region-{token}-{index + 1:00}-{cropToken}.png");
            var assertion = asserter.Assert(
                currentPath,
                rule.Region,
                rule.Assertion,
                baseline.ImagePath,
                cropPath,
                tolerance,
                rule.MinChangedPixels,
                rule.MostlyBlankMaxNonBlankPercent ?? 1);
            results.Add(new PreviewBaselineRegionCheckResult(
                index,
                rule.Region,
                rule.Assertion,
                ToToolResult(assertion)));
        }

        return results;
    }

    private static CoreResult<bool> WriteBaselineManifest(
        PreviewBaselineManifest manifest,
        string fullManifestPath)
    {
        try
        {
            var manifestDirectory = Path.GetDirectoryName(fullManifestPath);
            if (!string.IsNullOrWhiteSpace(manifestDirectory))
            {
                Directory.CreateDirectory(manifestDirectory);
            }

            File.WriteAllText(fullManifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            return CoreResult<bool>.Ok(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return CoreResult<bool>.Fail(new CoreError(
                CoreErrorCodes.PreviewBaselineFailed,
                $"Baseline manifest could not be written: {exception.Message}"));
        }
    }

    private static CoreResult<bool> WriteBaselineCheckReport(
        PreviewBaselineCheckResponse response,
        string reportPath)
    {
        try
        {
            var reportDirectory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrWhiteSpace(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            File.WriteAllText(reportPath, JsonSerializer.Serialize(response, JsonOptions));
            return CoreResult<bool>.Ok(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return CoreResult<bool>.Fail(new CoreError(
                CoreErrorCodes.PreviewBaselineFailed,
                $"Baseline check report could not be written: {exception.Message}"));
        }
    }

    private static CoreResult<PreviewBaselineManifest> LoadManifest(string manifestPath)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<PreviewBaselineManifest>(
                File.ReadAllText(manifestPath),
                JsonOptions);
            if (manifest is null)
            {
                return CoreResult<PreviewBaselineManifest>.Fail(new CoreError(
                    CoreErrorCodes.PreviewBaselineManifestInvalid,
                    "Baseline manifest did not contain a manifest object."));
            }

            if (manifest.Entries.Count == 0)
            {
                return CoreResult<PreviewBaselineManifest>.Fail(new CoreError(
                    CoreErrorCodes.PreviewBaselineManifestInvalid,
                    "Baseline manifest must contain at least one entry."));
            }

            return CoreResult<PreviewBaselineManifest>.Ok(manifest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return CoreResult<PreviewBaselineManifest>.Fail(new CoreError(
                CoreErrorCodes.PreviewBaselineManifestInvalid,
                $"Baseline manifest could not be loaded: {exception.Message}"));
        }
    }

    private static CoreResult<PreviewBaselineSuiteManifest> LoadSuiteManifest(string suiteManifestPath)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<PreviewBaselineSuiteManifest>(
                File.ReadAllText(suiteManifestPath),
                JsonOptions);
            if (manifest is null)
            {
                return CoreResult<PreviewBaselineSuiteManifest>.Fail(new CoreError(
                    CoreErrorCodes.PreviewBaselineManifestInvalid,
                    "Baseline suite manifest did not contain a manifest object."));
            }

            if (manifest.Entries.Count == 0)
            {
                return CoreResult<PreviewBaselineSuiteManifest>.Fail(new CoreError(
                    CoreErrorCodes.PreviewBaselineManifestInvalid,
                    "Baseline suite manifest must contain at least one entry."));
            }

            return CoreResult<PreviewBaselineSuiteManifest>.Ok(manifest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return CoreResult<PreviewBaselineSuiteManifest>.Fail(new CoreError(
                CoreErrorCodes.PreviewBaselineManifestInvalid,
                $"Baseline suite manifest could not be loaded: {exception.Message}"));
        }
    }

    private static CoreResult<IReadOnlyList<PreviewBaselineSuiteExpansion>> ExpandSuiteManifest(
        PreviewBaselineSuiteManifest suite,
        string fullSuiteManifestPath,
        string fullOutputDirectory)
    {
        var suiteDirectory = Path.GetDirectoryName(fullSuiteManifestPath) ?? Environment.CurrentDirectory;
        var presetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var preset in suite.MutationPresets)
        {
            if (!presetIds.Add(preset.Id))
            {
                return InvalidSuite($"Baseline suite mutation preset id '{preset.Id}' is duplicated.");
            }
        }

        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        var expansions = new List<PreviewBaselineSuiteExpansion>();
        foreach (var entry in suite.Entries)
        {
            if (!entryIds.Add(entry.Id))
            {
                return InvalidSuite($"Baseline suite entry id '{entry.Id}' is duplicated.");
            }

            var projectPath = ResolvePath(suiteDirectory, entry.ProjectPath);
            if (string.IsNullOrWhiteSpace(entry.ViewPath))
            {
                return InvalidSuite(
                    $"Baseline suite entry '{entry.Id}' must define a viewPath.",
                    new Dictionary<string, string>
                    {
                        ["suiteName"] = suite.Name,
                        ["entryId"] = entry.Id,
                        ["nextAction"] = "Add an explicit viewPath to the suite entry; profile references are recorded as metadata in this slice."
                    });
            }

            var profileFilePath = string.IsNullOrWhiteSpace(entry.ProfileFilePath)
                ? null
                : ResolvePath(suiteDirectory, entry.ProfileFilePath);
            var basePresetIds = MergePresetIds(suite.Defaults?.MutationPresetIds, entry.MutationPresetIds);
            var presetValidation = ValidatePresetReferences(suite, entry.Id, basePresetIds);
            if (!presetValidation.Success)
            {
                return CoreResult<IReadOnlyList<PreviewBaselineSuiteExpansion>>.Fail(presetValidation.Error!);
            }

            if (entry.Variants.Count > 0)
            {
                var defaultSize = SelectSizes(entry, suite.Defaults).FirstOrDefault();
                var defaultDpi = SelectDpis(entry, suite.Defaults)[0];
                var defaultTheme = SelectStrings(entry.Themes, suite.Defaults?.Themes)[0];
                var defaultCulture = SelectStrings(entry.Cultures, suite.Defaults?.Cultures)[0];
                var defaultDesignData = SelectStrings(entry.DesignDataTypes, suite.Defaults?.DesignDataTypes)[0];
                var defaultFrame = SelectFrames(entry, suite.Defaults)[0];
                for (var variantIndex = 0; variantIndex < entry.Variants.Count; variantIndex++)
                {
                    var variant = entry.Variants[variantIndex];
                    var viewport = variant.Size ?? defaultSize;
                    if (viewport is null)
                    {
                        return InvalidSuite(
                            $"Baseline suite entry '{entry.Id}' variant '{variant.Name ?? variantIndex.ToString(CultureInfo.InvariantCulture)}' must define a size or inherit one.");
                    }

                    var variantPresetIds = MergePresetIds(basePresetIds, variant.MutationPresetIds);
                    presetValidation = ValidatePresetReferences(suite, entry.Id, variantPresetIds);
                    if (!presetValidation.Success)
                    {
                        return CoreResult<IReadOnlyList<PreviewBaselineSuiteExpansion>>.Fail(presetValidation.Error!);
                    }

                    var dpi = variant.Dpi ?? defaultDpi;
                    var theme = variant.ThemeVariant ?? defaultTheme;
                    var culture = variant.Culture ?? defaultCulture;
                    var designData = variant.DesignDataType ?? defaultDesignData;
                    var frame = variant.AnimationTimeOffsetMs ?? defaultFrame;
                    var variantName = variant.Name ?? CreateVariantName(viewport, dpi, theme, culture, designData, frame);
                    var comparisonRules = MergeComparisonRules(
                        suite.Defaults?.ComparisonRules,
                        entry.ComparisonRules,
                        variant.ComparisonRules);
                    AddExpansion(
                        expansions,
                        suite,
                        entry,
                        variantName,
                        viewport,
                        dpi,
                        projectPath,
                        entry.ViewPath!,
                        fullOutputDirectory,
                        theme,
                        culture,
                        designData,
                        profileFilePath,
                        variant.RuntimeTarget ?? entry.RuntimeTarget,
                        variantPresetIds,
                        frame,
                        comparisonRules);
                }

                continue;
            }

            var sizes = SelectSizes(entry, suite.Defaults);
            if (sizes.Count == 0)
            {
                return InvalidSuite($"Baseline suite entry '{entry.Id}' must define at least one size or inherit default sizes.");
            }

            foreach (var size in sizes)
            foreach (var dpi in SelectDpis(entry, suite.Defaults))
            foreach (var theme in SelectStrings(entry.Themes, suite.Defaults?.Themes))
            foreach (var culture in SelectStrings(entry.Cultures, suite.Defaults?.Cultures))
            foreach (var designData in SelectStrings(entry.DesignDataTypes, suite.Defaults?.DesignDataTypes))
            foreach (var frame in SelectFrames(entry, suite.Defaults))
            {
                var variantName = CreateVariantName(size, dpi, theme, culture, designData, frame);
                var comparisonRules = MergeComparisonRules(
                    suite.Defaults?.ComparisonRules,
                    entry.ComparisonRules);
                AddExpansion(
                    expansions,
                    suite,
                    entry,
                    variantName,
                    size,
                    dpi,
                    projectPath,
                    entry.ViewPath!,
                    fullOutputDirectory,
                    theme,
                    culture,
                    designData,
                    profileFilePath,
                    entry.RuntimeTarget,
                    basePresetIds,
                    frame,
                    comparisonRules);
            }
        }

        if (expansions.Count == 0)
        {
            return InvalidSuite("Baseline suite manifest did not expand to any variants.");
        }

        return CoreResult<IReadOnlyList<PreviewBaselineSuiteExpansion>>.Ok(expansions);
    }

    private static void AddExpansion(
        List<PreviewBaselineSuiteExpansion> expansions,
        PreviewBaselineSuiteManifest suite,
        PreviewBaselineSuiteEntry entry,
        string variantName,
        PreviewViewport viewport,
        double dpi,
        string projectPath,
        string viewPath,
        string outputDirectory,
        string? theme,
        string? culture,
        string? designData,
        string? profileFilePath,
        RuntimeTargetContext? runtimeTarget,
        IReadOnlyList<string> mutationPresetIds,
        int? animationFrameMs,
        PreviewComparisonRules? comparisonRules)
    {
        var index = expansions.Count;
        var imagePath = CreateSuiteImagePath(
            outputDirectory,
            index,
            suite.Name,
            entry.Id,
            variantName,
            viewport,
            animationFrameMs);
        expansions.Add(new PreviewBaselineSuiteExpansion(
            index,
            suite.Name,
            entry.Id,
            variantName,
            viewport,
            imagePath,
            dpi,
            projectPath,
            viewPath,
            theme,
            culture,
            designData,
            entry.ProfileName,
            entry.ProfileVariant,
            profileFilePath,
            runtimeTarget,
            mutationPresetIds,
            animationFrameMs,
            comparisonRules));
    }

    private static CoreResult<IReadOnlyList<PreviewBaselineSuiteExpansion>> InvalidSuite(
        string message,
        IReadOnlyDictionary<string, string>? details = null)
    {
        return CoreResult<IReadOnlyList<PreviewBaselineSuiteExpansion>>.Fail(new CoreError(
            CoreErrorCodes.PreviewBaselineManifestInvalid,
            message,
            details));
    }

    private static CoreResult<bool> ValidatePresetReferences(
        PreviewBaselineSuiteManifest suite,
        string entryId,
        IReadOnlyList<string> presetIds)
    {
        if (presetIds.Count == 0)
        {
            return CoreResult<bool>.Ok(true);
        }

        var knownPresetIds = suite.MutationPresets.Select(static preset => preset.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var presetId in presetIds)
        {
            if (knownPresetIds.Contains(presetId))
            {
                continue;
            }

            return CoreResult<bool>.Fail(new CoreError(
                CoreErrorCodes.PreviewBaselineManifestInvalid,
                $"Baseline suite entry '{entryId}' references unknown mutation preset '{presetId}'.",
                new Dictionary<string, string>
                {
                    ["suiteName"] = suite.Name,
                    ["entryId"] = entryId,
                    ["mutationPresetId"] = presetId,
                    ["knownPresetIds"] = string.Join(",", knownPresetIds),
                    ["nextAction"] = "Declare the preset under mutationPresets or remove the mutationPresetIds reference."
                }));
        }

        return CoreResult<bool>.Ok(true);
    }

    private static IReadOnlyList<PreviewViewport> SelectSizes(
        PreviewBaselineSuiteEntry entry,
        PreviewBaselineSuiteDefaults? defaults)
    {
        if (entry.Sizes.Count > 0)
        {
            return entry.Sizes;
        }

        return defaults?.Sizes.Count > 0
            ? defaults.Sizes
            : [];
    }

    private static IReadOnlyList<double> SelectDpis(
        PreviewBaselineSuiteEntry entry,
        PreviewBaselineSuiteDefaults? defaults)
    {
        if (entry.Dpis.Count > 0)
        {
            return entry.Dpis;
        }

        return defaults?.Dpis.Count > 0
            ? defaults.Dpis
            : [96d];
    }

    private static IReadOnlyList<string?> SelectStrings(
        IReadOnlyList<string> entryValues,
        IReadOnlyList<string>? defaultValues)
    {
        if (entryValues.Count > 0)
        {
            return entryValues.Select(static value => (string?)value).ToArray();
        }

        return defaultValues?.Count > 0
            ? defaultValues.Select(static value => (string?)value).ToArray()
            : [(string?)null];
    }

    private static IReadOnlyList<int?> SelectFrames(
        PreviewBaselineSuiteEntry entry,
        PreviewBaselineSuiteDefaults? defaults)
    {
        if (entry.AnimationFramesMs.Count > 0)
        {
            return entry.AnimationFramesMs.Select(static value => (int?)value).ToArray();
        }

        return defaults?.AnimationFramesMs.Count > 0
            ? defaults.AnimationFramesMs.Select(static value => (int?)value).ToArray()
            : [(int?)null];
    }

    private static IReadOnlyList<string> MergePresetIds(
        IReadOnlyList<string>? first,
        IReadOnlyList<string>? second)
    {
        return (first ?? [])
            .Concat(second ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static PreviewComparisonRules? MergeComparisonRules(params PreviewComparisonRules?[] rules)
    {
        double? tolerance = null;
        long? maxChangedPixels = null;
        double? maxChangedPercent = null;
        var ignoredRegions = new List<ScreenshotRegion>();
        var requiredRegions = new List<PreviewRequiredRegion>();

        foreach (var rule in rules)
        {
            if (rule is null)
            {
                continue;
            }

            tolerance = rule.Tolerance ?? tolerance;
            maxChangedPixels = rule.MaxChangedPixels ?? maxChangedPixels;
            maxChangedPercent = rule.MaxChangedPercent ?? maxChangedPercent;
            ignoredRegions.AddRange(rule.IgnoredRegions);
            requiredRegions.AddRange(rule.RequiredRegions);
        }

        return tolerance is null
            && maxChangedPixels is null
            && maxChangedPercent is null
            && ignoredRegions.Count == 0
            && requiredRegions.Count == 0
                ? null
                : new PreviewComparisonRules(
                    tolerance,
                    maxChangedPixels,
                    maxChangedPercent,
                    ignoredRegions,
                    requiredRegions);
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(baseDirectory, path));
    }

    private static string CreateSuiteImagePath(
        string outputDirectory,
        int index,
        string suiteName,
        string entryId,
        string variantName,
        PreviewViewport viewport,
        int? animationFrameMs)
    {
        var frameSuffix = animationFrameMs is null
            ? string.Empty
            : $"-t{animationFrameMs.Value.ToString(CultureInfo.InvariantCulture)}ms";
        var token = string.Join(
            "-",
            [
                $"{index + 1:00}",
                Slug(suiteName),
                Slug(entryId),
                Slug(variantName),
                $"{FormatSize(viewport.Width)}x{FormatSize(viewport.Height)}{frameSuffix}"
            ]);

        return Path.Combine(outputDirectory, $"baseline-{token}.png");
    }

    private static string CreateVariantName(
        PreviewViewport viewport,
        double dpi,
        string? theme,
        string? culture,
        string? designData,
        int? animationFrameMs)
    {
        var parts = new List<string>
        {
            $"{FormatSize(viewport.Width)}x{FormatSize(viewport.Height)}",
            $"dpi{FormatSize(dpi)}"
        };

        if (!string.IsNullOrWhiteSpace(theme))
        {
            parts.Add(theme);
        }

        if (!string.IsNullOrWhiteSpace(culture))
        {
            parts.Add(culture);
        }

        if (!string.IsNullOrWhiteSpace(designData))
        {
            parts.Add(Path.GetFileName(designData));
        }

        if (animationFrameMs is not null)
        {
            parts.Add($"t{animationFrameMs.Value.ToString(CultureInfo.InvariantCulture)}ms");
        }

        return string.Join("-", parts);
    }

    private static string Slug(string value)
    {
        var chars = value
            .Trim()
            .Select(static character => char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(slug) ? "variant" : slug;
    }

    private static PreviewRequest CreateRequestForOutput(
        PreviewRequest request,
        string outputPath,
        double? width,
        double? height)
    {
        return new PreviewRequest(
            outputPath,
            width,
            height,
            request.Dpi,
            request.ProjectPath,
            request.ViewPath,
            request.ThemeVariant,
            request.Culture,
            request.DesignDataType);
    }

    private static ToolResult<T> ToToolResult<T>(CoreResult<T> result)
    {
        return result.Success
            ? ToolResult<T>.Ok(result.Value!)
            : ToolResult<T>.Fail(new ProtocolError(
                result.Error!.Code,
                result.Error.Message,
                result.Error.Details));
    }

    private static string CreateVariantToken(int index, PreviewViewport viewport)
    {
        return $"{index + 1:00}-{FormatSize(viewport.Width)}x{FormatSize(viewport.Height)}";
    }

    private static string FormatSize(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', '_');
    }
}
