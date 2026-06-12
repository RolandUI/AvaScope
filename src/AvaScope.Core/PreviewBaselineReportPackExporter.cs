using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewBaselineReportPackExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly TimeProvider _timeProvider;

    public PreviewBaselineReportPackExporter()
        : this(TimeProvider.System)
    {
    }

    public PreviewBaselineReportPackExporter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public CoreResult<AgentEvidenceReportPackResponse> Export(
        PreviewBaselineCheckResponse response,
        string reportDirectory)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (string.IsNullOrWhiteSpace(reportDirectory))
        {
            return Unavailable("Baseline report pack directory cannot be empty.");
        }

        string fullReportDirectory;
        try
        {
            fullReportDirectory = Path.GetFullPath(reportDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Unavailable($"Baseline report pack directory is invalid: {exception.Message}");
        }

        var generatedAt = _timeProvider.GetUtcNow();
        var entries = response.Entries.ToArray();
        var failedEntries = entries.Count(static entry => !EntryPassed(entry));
        var passedEntries = entries.Length - failedEntries;
        var jsonPath = Path.Combine(fullReportDirectory, "baseline-report.json");
        var htmlPath = Path.Combine(fullReportDirectory, "baseline-report.html");
        var junitPath = Path.Combine(fullReportDirectory, "baseline-junit.xml");
        var sarifPath = Path.Combine(fullReportDirectory, "baseline.sarif.json");
        var assets = new[]
        {
            new AgentEvidenceReportPackAsset("json", jsonPath, "application/json", "Machine-readable baseline check report."),
            new AgentEvidenceReportPackAsset("html", htmlPath, "text/html", "Human review report with grouped failures and image links."),
            new AgentEvidenceReportPackAsset("junit", junitPath, "application/xml", "JUnit-compatible status summary for CI systems."),
            new AgentEvidenceReportPackAsset("sarif", sarifPath, "application/sarif+json", "SARIF-style failure summary for code scanning surfaces.")
        };
        var metadata = CreateMetadata(response, entries);
        var environmentMetadata = CreateEnvironmentMetadata();
        var pack = new AgentEvidenceReportPackResponse(
            fullReportDirectory,
            failedEntries == 0 ? "passed" : "failed",
            generatedAt,
            entries.Length,
            passedEntries,
            failedEntries,
            assets,
            environmentMetadata,
            metadata);

        try
        {
            Directory.CreateDirectory(fullReportDirectory);
            File.WriteAllText(jsonPath, CreateJsonReport(response, pack), Encoding.UTF8);
            File.WriteAllText(htmlPath, CreateHtml(response, pack), Encoding.UTF8);
            CreateJUnit(response, pack).Save(junitPath);
            File.WriteAllText(sarifPath, CreateSarif(response, pack), Encoding.UTF8);
            return CoreResult<AgentEvidenceReportPackResponse>.Ok(pack);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return Unavailable($"Baseline report pack could not be written: {exception.Message}");
        }
    }

    private static string CreateJsonReport(
        PreviewBaselineCheckResponse response,
        AgentEvidenceReportPackResponse pack)
    {
        var report = new
        {
            reportPack = pack,
            baselineCheck = response,
            failures = response.Entries
                .Where(static entry => !EntryPassed(entry))
                .Select(static entry => new
                {
                    name = EntryName(entry),
                    suite = entry.Baseline.SuiteName,
                    entryId = entry.Baseline.SuiteEntryId,
                    variant = entry.Baseline.SuiteVariantName,
                    message = CreateFailureMessage(entry),
                    baselineImagePath = entry.Baseline.ImagePath,
                    currentImagePath = entry.CurrentImagePath,
                    diffPath = entry.DiffPath
                })
                .ToArray()
        };
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static string CreateHtml(
        PreviewBaselineCheckResponse response,
        AgentEvidenceReportPackResponse pack)
    {
        var failures = response.Entries.Where(static entry => !EntryPassed(entry)).ToArray();
        var failureGroups = failures.Length == 0
            ? "<p class=\"empty\">No baseline failures were reported.</p>"
            : string.Join(
                Environment.NewLine,
                failures
                    .GroupBy(static entry => entry.Baseline.SuiteName ?? "standalone")
                    .Select(CreateFailureGroupHtml));
        var entries = response.Entries.Count == 0
            ? "<p class=\"empty\">No baseline entries were checked.</p>"
            : string.Join(Environment.NewLine, response.Entries.Select(CreateEntryHtml));
        var assets = string.Join(Environment.NewLine, pack.Assets.Select(static asset =>
            $"<li><strong>{Html(asset.Kind)}</strong> <a href=\"{Html(asset.Url)}\">{Html(asset.Path)}</a></li>"));
        var metadata = CreateDictionaryHtml(pack.Metadata);
        var environment = CreateDictionaryHtml(pack.EnvironmentMetadata);

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>AvaScope Evidence Pack - {{Html(pack.Status)}}</title>
              <style>
                :root {
                  color-scheme: light dark;
                  --bg: #101411;
                  --panel: #18211b;
                  --panel-strong: #213026;
                  --text: #edf6ef;
                  --muted: #9fb1a4;
                  --border: #33453a;
                  --accent: #8bdc9f;
                  --bad: #ff7a70;
                  --good: #94e2a7;
                }

                * { box-sizing: border-box; }

                body {
                  margin: 0;
                  font-family: "Segoe UI", system-ui, sans-serif;
                  background:
                    radial-gradient(circle at 20% 0, rgba(139, 220, 159, 0.18), transparent 34rem),
                    linear-gradient(135deg, #0b0f0d, var(--bg));
                  color: var(--text);
                }

                main {
                  display: grid;
                  gap: 16px;
                  max-width: 1440px;
                  margin: 0 auto;
                  padding: 18px;
                }

                .hero,
                .panel,
                .entry,
                figure {
                  border: 1px solid var(--border);
                  border-radius: 12px;
                  background: color-mix(in srgb, var(--panel) 92%, transparent);
                  box-shadow: 0 16px 48px rgba(0, 0, 0, 0.25);
                }

                .hero {
                  display: grid;
                  grid-template-columns: minmax(0, 1fr) minmax(280px, 420px);
                  gap: 20px;
                  padding: 22px;
                }

                .panel,
                .entry {
                  padding: 16px;
                }

                .eyebrow {
                  color: var(--accent);
                  font-size: 12px;
                  font-weight: 700;
                  letter-spacing: 0.12em;
                  margin: 0 0 10px;
                  text-transform: uppercase;
                }

                h1 {
                  font-size: clamp(24px, 4vw, 44px);
                  line-height: 1;
                  margin: 0;
                }

                h2 {
                  color: var(--accent);
                  font-size: 16px;
                  margin: 0 0 12px;
                }

                h3 {
                  margin: 0 0 10px;
                }

                .lead {
                  color: var(--muted);
                  font-size: 16px;
                  margin: 14px 0 0;
                }

                .facts {
                  display: grid;
                  grid-template-columns: 150px minmax(0, 1fr);
                  gap: 8px 12px;
                  margin: 0;
                }

                dt { color: var(--muted); }
                dd { margin: 0; overflow-wrap: anywhere; }

                .entry {
                  display: grid;
                  gap: 12px;
                  margin-top: 12px;
                }

                .entry.pass { border-color: color-mix(in srgb, var(--good) 50%, var(--border)); }
                .entry.fail { border-color: color-mix(in srgb, var(--bad) 60%, var(--border)); }

                .badge {
                  border-radius: 999px;
                  display: inline-block;
                  font-size: 12px;
                  font-weight: 700;
                  padding: 2px 8px;
                  width: fit-content;
                }

                .badge.pass { background: var(--good); color: #0b0f0d; }
                .badge.fail { background: var(--bad); color: #0b0f0d; }

                .image-grid {
                  display: grid;
                  grid-template-columns: repeat(3, minmax(0, 1fr));
                  gap: 12px;
                }

                figure {
                  margin: 0;
                  padding: 12px;
                  min-width: 0;
                }

                figure img {
                  display: block;
                  width: 100%;
                  height: auto;
                  border: 1px solid var(--border);
                  border-radius: 8px;
                  background: #fff;
                }

                figcaption,
                .empty {
                  color: var(--muted);
                  overflow-wrap: anywhere;
                }

                a { color: var(--accent); }

                pre {
                  white-space: pre-wrap;
                  overflow-wrap: anywhere;
                  border: 1px solid var(--border);
                  border-radius: 8px;
                  padding: 12px;
                  background: #0a0e0c;
                  color: #dce9df;
                }

                @media (max-width: 900px) {
                  .hero,
                  .image-grid {
                    grid-template-columns: 1fr;
                  }
                }
              </style>
            </head>
            <body>
              <main>
                <section class="hero">
                  <div>
                    <p class="eyebrow">AvaScope agent evidence pack</p>
                    <h1>Baseline check {{Html(pack.Status)}}</h1>
                    <p class="lead">{{pack.TotalEntries}} checked, {{pack.PassedEntries}} passed, {{pack.FailedEntries}} failed.</p>
                  </div>
                  <dl class="facts">
                    <dt>Manifest</dt><dd>{{Html(response.ManifestPath)}}</dd>
                    <dt>Checked</dt><dd>{{Html(response.CheckedAt.ToString("O", CultureInfo.InvariantCulture))}}</dd>
                    <dt>Generated</dt><dd>{{Html(pack.GeneratedAt.ToString("O", CultureInfo.InvariantCulture))}}</dd>
                    <dt>Status</dt><dd>{{Html(pack.Status)}}</dd>
                  </dl>
                </section>
                <section class="panel">
                  <h2>Grouped Failures</h2>
                  {{failureGroups}}
                </section>
                <section class="panel">
                  <h2>Checked Entries</h2>
                  {{entries}}
                </section>
                <section class="panel">
                  <h2>Pack Assets</h2>
                  <ul>{{assets}}</ul>
                </section>
                <section class="panel">
                  <h2>Metadata</h2>
                  {{metadata}}
                  <h2>Environment</h2>
                  {{environment}}
                </section>
              </main>
            </body>
            </html>
            """;
    }

    private static XDocument CreateJUnit(
        PreviewBaselineCheckResponse response,
        AgentEvidenceReportPackResponse pack)
    {
        var testsuite = new XElement(
            "testsuite",
            new XAttribute("name", "AvaScope baseline check"),
            new XAttribute("tests", pack.TotalEntries.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("failures", pack.FailedEntries.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("errors", "0"),
            new XAttribute("time", "0"),
            new XAttribute("timestamp", response.CheckedAt.ToString("O", CultureInfo.InvariantCulture)));

        foreach (var entry in response.Entries)
        {
            var testcase = new XElement(
                "testcase",
                new XAttribute("classname", JUnitClassName(entry)),
                new XAttribute("name", EntryName(entry)),
                new XAttribute("time", "0"));
            if (!EntryPassed(entry))
            {
                testcase.Add(new XElement(
                    "failure",
                    new XAttribute("message", CreateFailureMessage(entry)),
                    CreateFailureDetails(entry)));
            }

            testsuite.Add(testcase);
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), testsuite);
    }

    private static string CreateSarif(
        PreviewBaselineCheckResponse response,
        AgentEvidenceReportPackResponse pack)
    {
        var results = response.Entries
            .Where(static entry => !EntryPassed(entry))
            .Select(static entry => new
            {
                ruleId = "avascope.baseline.check",
                level = "error",
                message = new
                {
                    text = CreateFailureMessage(entry)
                },
                locations = new[]
                {
                    new
                    {
                        physicalLocation = new
                        {
                            artifactLocation = new
                            {
                                uri = ToUri(string.IsNullOrWhiteSpace(entry.Diff.Value?.DiffPath)
                                    ? entry.CurrentImagePath
                                    : entry.Diff.Value!.DiffPath!)
                            }
                        }
                    }
                },
                properties = new
                {
                    entryName = EntryName(entry),
                    suite = entry.Baseline.SuiteName,
                    suiteEntryId = entry.Baseline.SuiteEntryId,
                    suiteVariantName = entry.Baseline.SuiteVariantName,
                    baselineImagePath = entry.Baseline.ImagePath,
                    currentImagePath = entry.CurrentImagePath,
                    diffPath = entry.DiffPath
                }
            })
            .ToArray();
        var sarif = new Dictionary<string, object?>
        {
            ["version"] = "2.1.0",
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["runs"] = new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "AvaScope",
                            informationUri = "https://github.com/RolandUI/AvaScope",
                            rules = new[]
                            {
                                new
                                {
                                    id = "avascope.baseline.check",
                                    shortDescription = new
                                    {
                                        text = "AvaScope baseline check failure"
                                    }
                                }
                            }
                        }
                    },
                    invocations = new[]
                    {
                        new
                        {
                            executionSuccessful = pack.FailedEntries == 0,
                            endTimeUtc = pack.GeneratedAt.ToString("O", CultureInfo.InvariantCulture)
                        }
                    },
                    results
                }
            }
        };
        return JsonSerializer.Serialize(sarif, JsonOptions);
    }

    private static string CreateFailureGroupHtml(IGrouping<string, PreviewBaselineCheckEntry> group)
    {
        return $$"""
            <div class="entry fail">
              <h3>{{Html(group.Key)}}</h3>
              {{string.Join(Environment.NewLine, group.Select(static entry => $"<p><strong>{Html(EntryName(entry))}</strong>: {Html(CreateFailureMessage(entry))}</p>"))}}
            </div>
            """;
    }

    private static string CreateEntryHtml(PreviewBaselineCheckEntry entry)
    {
        var passed = EntryPassed(entry);
        var status = passed ? "pass" : "fail";
        var badge = passed ? "passed" : "failed";
        var summary = passed ? "Entry matched its baseline rules." : CreateFailureMessage(entry);
        var mutationPresets = entry.Baseline.MutationPresetIds.Count == 0
            ? "-"
            : string.Join(", ", entry.Baseline.MutationPresetIds);
        var requiredRegions = entry.RequiredRegionResults.Count == 0
            ? "<p class=\"empty\">No required regions were evaluated.</p>"
            : string.Join(Environment.NewLine, entry.RequiredRegionResults.Select(CreateRequiredRegionHtml));

        return $$"""
            <div class="entry {{status}}">
              <span class="badge {{status}}">{{Html(badge)}}</span>
              <h3>{{Html(EntryName(entry))}}</h3>
              <p>{{Html(summary)}}</p>
              <dl class="facts">
                <dt>Viewport</dt><dd>{{FormatSize(entry.Baseline.Viewport.Width)}} x {{FormatSize(entry.Baseline.Viewport.Height)}} @ {{entry.Baseline.Dpi.ToString("0.###", CultureInfo.InvariantCulture)}} DPI</dd>
                <dt>Theme</dt><dd>{{Html(entry.Baseline.ThemeVariant ?? "-")}}</dd>
                <dt>Culture</dt><dd>{{Html(entry.Baseline.Culture ?? "-")}}</dd>
                <dt>Animation</dt><dd>{{Html(entry.Baseline.AnimationTimeOffsetMs?.ToString(CultureInfo.InvariantCulture) ?? "-")}}</dd>
                <dt>Mutation presets</dt><dd>{{Html(mutationPresets)}}</dd>
                <dt>Changed pixels</dt><dd>{{Html(entry.Diff.Value?.ChangedPixels.ToString(CultureInfo.InvariantCulture) ?? "-")}}</dd>
                <dt>Changed percent</dt><dd>{{Html(entry.Diff.Value?.ChangedPercent.ToString("0.####", CultureInfo.InvariantCulture) ?? "-")}}</dd>
              </dl>
              <div class="image-grid">
                {{CreateImageFigure("Baseline", entry.Baseline.ImagePath)}}
                {{CreateImageFigure("Current", entry.CurrentImagePath)}}
                {{CreateImageFigure("Diff", entry.Diff.Value?.DiffPath ?? entry.DiffPath)}}
              </div>
              <section>
                <h2>Required Regions</h2>
                {{requiredRegions}}
              </section>
            </div>
            """;
    }

    private static string CreateImageFigure(string label, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var content = File.Exists(fullPath)
            ? $"<img alt=\"{Html(label)} image\" src=\"{Html(ToUri(fullPath))}\">"
            : "<p class=\"empty\">Artifact file was not found on disk.</p>";

        return $$"""
            <figure>
              <h2>{{Html(label)}}</h2>
              {{content}}
              <figcaption>{{Html(fullPath)}}</figcaption>
            </figure>
            """;
    }

    private static string CreateRequiredRegionHtml(PreviewBaselineRegionCheckResult result)
    {
        var passed = result.Result.Success && result.Result.Value is { Passed: true };
        var status = passed ? "pass" : "fail";
        var message = result.Result.Success
            ? CreateRegionStatus(result.Result.Value!)
            : result.Result.Error!.Message;
        return $$"""
            <div class="entry {{status}}">
              <span class="badge {{status}}">{{Html(status == "pass" ? "passed" : "failed")}}</span>
              <strong>{{Html(result.Region.Name ?? result.Assertion)}}</strong>
              <span>{{Html(message)}}</span>
              <span class="empty">{{Html(result.Result.Value?.CropPath ?? "-")}}</span>
            </div>
            """;
    }

    private static string CreateDictionaryHtml(IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0)
        {
            return "<p class=\"empty\">No metadata was recorded.</p>";
        }

        var rows = string.Join(Environment.NewLine, values.Select(static pair =>
            $"<dt>{Html(pair.Key)}</dt><dd>{Html(pair.Value)}</dd>"));
        return $"<dl class=\"facts\">{rows}</dl>";
    }

    private static IReadOnlyDictionary<string, string> CreateEnvironmentMetadata()
    {
        return new Dictionary<string, string>
        {
            ["os"] = RuntimeInformation.OSDescription,
            ["framework"] = RuntimeInformation.FrameworkDescription,
            ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["processorCount"] = Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static IReadOnlyDictionary<string, string> CreateMetadata(
        PreviewBaselineCheckResponse response,
        IReadOnlyList<PreviewBaselineCheckEntry> entries)
    {
        var suiteCount = entries
            .Select(static entry => entry.Baseline.SuiteName)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var mutationPresetCount = entries
            .SelectMany(static entry => entry.Baseline.MutationPresetIds)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var runtimeTargetCount = entries.Count(static entry => entry.Baseline.RuntimeTarget is not null);
        var animationFrameCount = entries.Count(static entry => entry.Baseline.AnimationTimeOffsetMs is not null);
        return new Dictionary<string, string>
        {
            ["kind"] = "preview-baseline-check",
            ["manifestPath"] = response.ManifestPath,
            ["checkedAt"] = response.CheckedAt.ToString("O", CultureInfo.InvariantCulture),
            ["suiteCount"] = suiteCount.ToString(CultureInfo.InvariantCulture),
            ["mutationPresetCount"] = mutationPresetCount.ToString(CultureInfo.InvariantCulture),
            ["runtimeTargetCount"] = runtimeTargetCount.ToString(CultureInfo.InvariantCulture),
            ["animationFrameCount"] = animationFrameCount.ToString(CultureInfo.InvariantCulture),
            ["mutationHistoryStatus"] = mutationPresetCount == 0
                ? "not_collected"
                : "preset_metadata_available"
        };
    }

    private static bool EntryPassed(PreviewBaselineCheckEntry entry)
    {
        return entry.Render.Success
            && entry.Diff.Success
            && entry.Diff.Value is { Passed: true }
            && entry.RequiredRegionResults.All(static region => region.Result.Success && region.Result.Value is { Passed: true });
    }

    private static string EntryName(PreviewBaselineCheckEntry entry)
    {
        var parts = new[]
        {
            entry.Baseline.SuiteName,
            entry.Baseline.SuiteEntryId,
            entry.Baseline.SuiteVariantName
        }.Where(static part => !string.IsNullOrWhiteSpace(part));
        var name = string.Join(" / ", parts);
        return string.IsNullOrWhiteSpace(name)
            ? $"entry-{entry.Baseline.Index + 1:00}-{FormatSize(entry.Baseline.Viewport.Width)}x{FormatSize(entry.Baseline.Viewport.Height)}"
            : name;
    }

    private static string JUnitClassName(PreviewBaselineCheckEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.Baseline.SuiteName)
            ? "AvaScope.Baseline"
            : $"AvaScope.Baseline.{entry.Baseline.SuiteName}";
    }

    private static string CreateFailureMessage(PreviewBaselineCheckEntry entry)
    {
        if (!entry.Render.Success)
        {
            return $"Render failed: {entry.Render.Error!.Message}";
        }

        if (!entry.Diff.Success)
        {
            return $"Diff failed: {entry.Diff.Error!.Message}";
        }

        if (entry.Diff.Value is not null && !entry.Diff.Value.Passed)
        {
            return $"Image changed: {entry.Diff.Value.ChangedPixels.ToString(CultureInfo.InvariantCulture)} pixels ({entry.Diff.Value.ChangedPercent.ToString("0.####", CultureInfo.InvariantCulture)}%).";
        }

        var failedRegion = entry.RequiredRegionResults.FirstOrDefault(static region =>
            !region.Result.Success || region.Result.Value is not { Passed: true });
        if (failedRegion is not null)
        {
            var label = failedRegion.Region.Name ?? failedRegion.Assertion;
            var message = failedRegion.Result.Success
                ? CreateRegionStatus(failedRegion.Result.Value!)
                : failedRegion.Result.Error!.Message;
            return $"Required region '{label}' failed: {message}";
        }

        return "Entry failed baseline validation.";
    }

    private static string CreateFailureDetails(PreviewBaselineCheckEntry entry)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"name: {EntryName(entry)}",
                $"baseline: {entry.Baseline.ImagePath}",
                $"current: {entry.CurrentImagePath}",
                $"diff: {entry.DiffPath}",
                $"message: {CreateFailureMessage(entry)}"
            ]);
    }

    private static string CreateRegionStatus(ScreenshotRegionAssertionResponse response)
    {
        return response.Passed
            ? $"{response.Assertion} passed."
            : $"{response.Assertion} failed.";
    }

    private static string ToUri(string path)
    {
        return new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }

    private static CoreResult<AgentEvidenceReportPackResponse> Unavailable(string message)
    {
        return CoreResult<AgentEvidenceReportPackResponse>.Fail(new CoreError(
            CoreErrorCodes.AgentEvidenceReportPackUnavailable,
            message));
    }

    private static string Html(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string FormatSize(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', '_');
    }
}
