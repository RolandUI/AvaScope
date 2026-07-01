using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class PreviewBaselineManagerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [Fact]
    public void ExpandSuiteManifestCreatesDeterministicVariantsAndMetadata()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        var suitePath = Path.Combine(testRoot, "agent-suite.json");
        var outputDirectory = Path.Combine(testRoot, "baseline-images");
        var target = new RuntimeTargetContext(
            new SessionId("session-suite"),
            "topLevel:main",
            TreeKinds.Visual,
            "visual:root");
        var suite = new PreviewBaselineSuiteManifest(
            PreviewBaselineSuiteManifest.CurrentVersion,
            "Agent Suite",
            [
                new PreviewBaselineSuiteEntry(
                    "main",
                    "Sample.csproj",
                    "Views/MainView.axaml",
                    "main-profile",
                    "compact",
                    "avascope.preview.json",
                    sizes: [new PreviewViewport(120, 70)],
                    runtimeTarget: target,
                    comparisonRules: new PreviewComparisonRules(
                        maxChangedPixels: 4,
                        requiredRegions:
                        [
                            new PreviewRequiredRegion(new ScreenshotRegion(0, 0, 12, 8, "hero"))
                        ]))
            ],
            new PreviewBaselineSuiteDefaults(
                dpis: [96],
                themes: ["light", "dark"],
                cultures: ["en-US"],
                animationFramesMs: [0, 125],
                mutationPresetIds: ["wide"],
                comparisonRules: new PreviewComparisonRules(
                    tolerance: 2,
                    ignoredRegions:
                    [
                        new ScreenshotRegion(0, 0, 4, 4, "clock")
                    ])),
            [
                new PreviewBaselineMutationPreset(
                    "wide",
                    "Metadata-only width mutation preset.",
                    [
                        new RuntimeMutationOperation(
                            RuntimeMutationOperationKinds.SetProperty,
                            propertyName: "Width",
                            value: "120",
                            valueType: "double")
                    ])
            ]);
        File.WriteAllText(suitePath, JsonSerializer.Serialize(suite, JsonOptions));

        try
        {
            var result = new PreviewBaselineManager(new PreviewHostClient()).ExpandSuiteManifest(
                suitePath,
                outputDirectory);

            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(4, result.Value!.Count);
            var first = result.Value[0];
            Assert.Equal(0, first.Index);
            Assert.Equal("Agent Suite", first.SuiteName);
            Assert.Equal("main", first.EntryId);
            Assert.Equal("120x70-dpi96-light-en-US-t0ms", first.VariantName);
            Assert.Equal(120, first.Viewport.Width);
            Assert.Equal(70, first.Viewport.Height);
            Assert.Equal(Path.Combine(testRoot, "Sample.csproj"), first.ProjectPath);
            Assert.Equal("Views/MainView.axaml", first.ViewPath);
            Assert.Equal("light", first.ThemeVariant);
            Assert.Equal("en-US", first.Culture);
            Assert.Equal("main-profile", first.ProfileName);
            Assert.Equal("compact", first.ProfileVariant);
            Assert.Equal(Path.Combine(testRoot, "avascope.preview.json"), first.ProfileFilePath);
            Assert.Equal(target, first.RuntimeTarget);
            Assert.Equal("wide", Assert.Single(first.MutationPresetIds));
            Assert.Equal(0, first.AnimationTimeOffsetMs);
            Assert.NotNull(first.ComparisonRules);
            Assert.Equal(2, first.ComparisonRules!.Tolerance);
            Assert.Equal(4, first.ComparisonRules.MaxChangedPixels);
            Assert.Equal("clock", Assert.Single(first.ComparisonRules.IgnoredRegions).Name);
            Assert.Equal("hero", Assert.Single(first.ComparisonRules.RequiredRegions).Region.Name);
            Assert.Equal(
                Path.Combine(outputDirectory, "baseline-01-agent-suite-main-120x70-dpi96-light-en-us-t0ms-120x70-t0ms.png"),
                first.ImagePath);

            var last = result.Value[3];
            Assert.Equal("dark", last.ThemeVariant);
            Assert.Equal(125, last.AnimationTimeOffsetMs);
            Assert.EndsWith(
                "baseline-04-agent-suite-main-120x70-dpi96-dark-en-us-t125ms-120x70-t125ms.png",
                last.ImagePath,
                StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExpandSuiteManifestCarriesExplicitStateVariant()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        var suitePath = Path.Combine(testRoot, "state-suite.json");
        var outputDirectory = Path.Combine(testRoot, "baseline-images");
        var suite = new PreviewBaselineSuiteManifest(
            PreviewBaselineSuiteManifest.CurrentVersion,
            "State Suite",
            [
                new PreviewBaselineSuiteEntry(
                    "main",
                    "Sample.csproj",
                    "Views/MainView.axaml",
                    variants:
                    [
                        new PreviewBaselineSuiteVariant(
                            size: new PreviewViewport(320, 180),
                            dpi: 96,
                            themeVariant: "light",
                            culture: "en-US",
                            designDataType: "Sample.PreviewDesignData",
                            stateVariant: "loading")
                    ])
            ]);
        File.WriteAllText(suitePath, JsonSerializer.Serialize(suite, JsonOptions));

        try
        {
            var result = new PreviewBaselineManager(new PreviewHostClient()).ExpandSuiteManifest(
                suitePath,
                outputDirectory);

            Assert.True(result.Success, result.Error?.Message);
            var expansion = Assert.Single(result.Value!);
            Assert.Equal("loading", expansion.StateVariant);
            Assert.Equal("320x180-dpi96-light-en-US-Sample.PreviewDesignData-loading", expansion.VariantName);
            Assert.EndsWith(
                "baseline-01-state-suite-main-320x180-dpi96-light-en-us-sample-previewdesigndata-loading-320x180.png",
                expansion.ImagePath,
                StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExpandSuiteManifestReportsUnknownMutationPresetReference()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        var suitePath = Path.Combine(testRoot, "agent-suite.json");
        var suite = new PreviewBaselineSuiteManifest(
            PreviewBaselineSuiteManifest.CurrentVersion,
            "Agent Suite",
            [
                new PreviewBaselineSuiteEntry(
                    "main",
                    "Sample.csproj",
                    "Views/MainView.axaml",
                    sizes: [new PreviewViewport(120, 70)],
                    mutationPresetIds: ["missing"])
            ]);
        File.WriteAllText(suitePath, JsonSerializer.Serialize(suite, JsonOptions));

        try
        {
            var result = new PreviewBaselineManager(new PreviewHostClient()).ExpandSuiteManifest(
                suitePath,
                Path.Combine(testRoot, "baseline-images"));

            Assert.False(result.Success);
            Assert.Equal(CoreErrorCodes.PreviewBaselineManifestInvalid, result.Error!.Code);
            Assert.Contains("unknown mutation preset", result.Error.Message, StringComparison.Ordinal);
            Assert.Equal("missing", result.Error.Details!["mutationPresetId"]);
            Assert.Equal("main", result.Error.Details["entryId"]);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
