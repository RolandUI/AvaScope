using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class RuntimeMutationReviewExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly TimeProvider _timeProvider;

    public RuntimeMutationReviewExporter()
        : this(TimeProvider.System)
    {
    }

    public RuntimeMutationReviewExporter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public CoreResult<RuntimeMutationReviewArtifact> ExportEvidence(
        RuntimeMutationEvidenceResponse evidence,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        string artifactPath;
        try
        {
            artifactPath = string.IsNullOrWhiteSpace(outputPath)
                ? Path.Combine(evidence.ArtifactDirectory, $"{SanitizeFileName(evidence.RequestId)}-review.html")
                : Path.GetFullPath(outputPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Unavailable($"Runtime mutation review artifact path is invalid: {exception.Message}");
        }

        return WriteArtifact(artifactPath, CreateEvidenceHtml(evidence));
    }

    public CoreResult<RuntimeMutationReviewArtifact> ExportReview(
        RuntimeMutationReviewResponse review,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(review);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Unavailable("Runtime mutation review output path cannot be empty.");
        }

        string artifactPath;
        try
        {
            artifactPath = Path.GetFullPath(outputPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Unavailable($"Runtime mutation review artifact path is invalid: {exception.Message}");
        }

        return WriteArtifact(artifactPath, CreateReviewHtml(review));
    }

    private CoreResult<RuntimeMutationReviewArtifact> WriteArtifact(string artifactPath, string html)
    {
        try
        {
            var directory = Path.GetDirectoryName(artifactPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(artifactPath, html, Encoding.UTF8);
            return CoreResult<RuntimeMutationReviewArtifact>.Ok(new RuntimeMutationReviewArtifact(
                artifactPath,
                new Uri(artifactPath).AbsoluteUri,
                "html",
                _timeProvider.GetUtcNow()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return Unavailable($"Runtime mutation review artifact could not be written: {exception.Message}");
        }
    }

    private static string CreateEvidenceHtml(RuntimeMutationEvidenceResponse evidence)
    {
        var title = $"Runtime mutation evidence - {evidence.Mutation.MutationId}";
        var diagnostics = CreateDiagnosticsHtml(evidence.Diagnostics.Concat(evidence.Mutation.Diagnostics));
        var beforeFigure = CreateImageFigure("Before", evidence.BeforeScreenshotPath);
        var afterFigure = CreateImageFigure("After", evidence.AfterScreenshotPath);
        var diffFigure = CreateImageFigure("Diff", evidence.DiffPath);
        var beforeTarget = evidence.BeforeTarget is null
            ? "<p class=\"empty\">Before target was not found in the captured visual tree.</p>"
            : CreateTargetHtml(evidence.BeforeTarget);
        var afterTarget = evidence.AfterTarget is null
            ? "<p class=\"empty\">After target was not found in the captured visual tree.</p>"
            : CreateTargetHtml(evidence.AfterTarget);
        var json = JsonSerializer.Serialize(evidence, JsonOptions);

        return WrapHtml(
            title,
            $$"""
            <section class="hero">
              <div>
                <p class="eyebrow">AvaScope runtime experiment review</p>
                <h1>{{Html(title)}}</h1>
                <p class="lead">Mutation {{Html(evidence.Mutation.Status)}}; evidence {{Html(evidence.Summary.Status)}}; diff {{Html(evidence.Summary.DiffStatus)}}.</p>
              </div>
              <dl class="facts">
                <dt>Session</dt><dd>{{Html(evidence.SessionId.Value)}}</dd>
                <dt>Top-level</dt><dd>{{Html(evidence.TopLevelId)}}</dd>
                <dt>Request</dt><dd>{{Html(evidence.RequestId)}}</dd>
                <dt>Mutation</dt><dd>{{Html(evidence.Mutation.MutationId)}}</dd>
                <dt>Operation</dt><dd>{{Html(evidence.Mutation.Operation.Kind)}}</dd>
                <dt>Captured</dt><dd>{{Html(evidence.CapturedAt.ToString("O", CultureInfo.InvariantCulture))}}</dd>
              </dl>
            </section>
            <section class="image-grid">
              {{beforeFigure}}
              {{afterFigure}}
              {{diffFigure}}
            </section>
            <section class="panel">
              <h2>Target Before</h2>
              {{beforeTarget}}
              <h2>Target After</h2>
              {{afterTarget}}
            </section>
            <section class="panel">
              <h2>Reset Handoff</h2>
              <dl class="facts">
                <dt>Mutation id</dt><dd>{{Html(evidence.Mutation.MutationId)}}</dd>
                <dt>Reset one</dt><dd>{{Html(RuntimeMutationOperationKinds.ResetMutation)}}</dd>
                <dt>Reset all</dt><dd>{{Html(RuntimeMutationOperationKinds.ResetAll)}}</dd>
                <dt>Active count</dt><dd>{{Html(evidence.Mutation.Metadata.TryGetValue("activeMutationCount", out var activeCount) ? activeCount : "unknown")}}</dd>
              </dl>
            </section>
            <section class="panel">
              <h2>Diagnostics</h2>
              {{diagnostics}}
            </section>
            <section class="panel">
              <h2>Evidence JSON</h2>
              <pre>{{Html(json)}}</pre>
            </section>
            """);
    }

    private static string CreateReviewHtml(RuntimeMutationReviewResponse review)
    {
        var title = $"Runtime mutation review - {review.SessionId.Value}";
        var activeRows = CreateEntryRows(review.ActiveMutations);
        var sourceRows = CreateSourceSuggestionRows(review.SourceSuggestions);
        var historyRows = CreateEntryRows(review.History);
        var json = JsonSerializer.Serialize(review, JsonOptions);

        return WrapHtml(
            title,
            $$"""
            <section class="hero">
              <div>
                <p class="eyebrow">AvaScope runtime experiment review</p>
                <h1>{{Html(title)}}</h1>
                <p class="lead">{{review.ActiveMutationCount}} active mutation(s), {{review.HistoryCount}} history item(s).</p>
              </div>
              <dl class="facts">
                <dt>Session</dt><dd>{{Html(review.SessionId.Value)}}</dd>
                <dt>Reviewed</dt><dd>{{Html(review.ReviewedAt.ToString("O", CultureInfo.InvariantCulture))}}</dd>
                <dt>Reset one</dt><dd>{{Html(review.ResetHandoff.ResetMutationOperation)}}</dd>
                <dt>Reset all</dt><dd>{{Html(review.ResetHandoff.ResetAllOperation)}}</dd>
              </dl>
            </section>
            <section class="panel">
              <h2>Active Mutations</h2>
              {{activeRows}}
            </section>
            <section class="panel">
              <h2>Source Suggestions</h2>
              {{sourceRows}}
            </section>
            <section class="panel">
              <h2>History</h2>
              {{historyRows}}
            </section>
            <section class="panel">
              <h2>Review JSON</h2>
              <pre>{{Html(json)}}</pre>
            </section>
            """);
    }

    private static string WrapHtml(string title, string body)
    {
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{Html(title)}}</title>
              <style>
                :root {
                  color-scheme: light dark;
                  --bg: #0f1411;
                  --panel: #19211c;
                  --panel-strong: #202b24;
                  --text: #eef6ef;
                  --muted: #9fb0a4;
                  --border: #334339;
                  --accent: #8bdc9f;
                  --warning: #ffd166;
                }

                * { box-sizing: border-box; }

                body {
                  margin: 0;
                  font-family: "Segoe UI", system-ui, sans-serif;
                  background:
                    radial-gradient(circle at top left, rgba(139, 220, 159, 0.18), transparent 34rem),
                    linear-gradient(135deg, #0b0f0d, var(--bg));
                  color: var(--text);
                }

                main {
                  display: grid;
                  gap: 16px;
                  padding: 18px;
                  max-width: 1400px;
                  margin: 0 auto;
                }

                .hero,
                .panel,
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
                  font-size: 15px;
                  margin: 0 0 12px;
                }

                .lead {
                  color: var(--muted);
                  font-size: 16px;
                  margin: 14px 0 0;
                }

                .facts {
                  display: grid;
                  grid-template-columns: 120px minmax(0, 1fr);
                  gap: 8px 12px;
                  margin: 0;
                }

                dt { color: var(--muted); }
                dd { margin: 0; overflow-wrap: anywhere; }

                .image-grid {
                  display: grid;
                  grid-template-columns: repeat(3, minmax(0, 1fr));
                  gap: 16px;
                }

                figure {
                  margin: 0;
                  padding: 14px;
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

                figcaption {
                  color: var(--muted);
                  margin-top: 10px;
                  overflow-wrap: anywhere;
                }

                .panel {
                  padding: 16px;
                  min-width: 0;
                }

                .entry {
                  border-top: 1px solid var(--border);
                  display: grid;
                  gap: 4px;
                  padding: 12px 0;
                }

                .entry:first-of-type { border-top: 0; }

                .badge {
                  color: #0b0f0d;
                  background: var(--accent);
                  border-radius: 999px;
                  display: inline-block;
                  font-size: 12px;
                  font-weight: 700;
                  padding: 2px 8px;
                  width: fit-content;
                }

                .empty { color: var(--muted); }

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
                {{body}}
              </main>
            </body>
            </html>
            """;
    }

    private static string CreateImageFigure(string label, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return $$"""
                <figure>
                  <h2>{{Html(label)}}</h2>
                  <p class="empty">No artifact path was provided.</p>
                </figure>
                """;
        }

        var fullPath = Path.GetFullPath(path);
        var image = File.Exists(fullPath)
            ? $"<img alt=\"{Html(label)} screenshot\" src=\"{Html(new Uri(fullPath).AbsoluteUri)}\">"
            : "<p class=\"empty\">Artifact file was not found on disk.</p>";

        return $$"""
            <figure>
              <h2>{{Html(label)}}</h2>
              {{image}}
              <figcaption>{{Html(fullPath)}}</figcaption>
            </figure>
            """;
    }

    private static string CreateTargetHtml(RuntimeMutationEvidenceTargetSummary target)
    {
        var classes = target.Classes.Count == 0 ? "-" : string.Join(", ", target.Classes);
        var bounds = target.Bounds is null
            ? "-"
            : $"{target.Bounds.X.ToString("0.###", CultureInfo.InvariantCulture)}, {target.Bounds.Y.ToString("0.###", CultureInfo.InvariantCulture)}, {target.Bounds.Width.ToString("0.###", CultureInfo.InvariantCulture)} x {target.Bounds.Height.ToString("0.###", CultureInfo.InvariantCulture)}";

        return $$"""
            <dl class="facts">
              <dt>Node</dt><dd>{{Html(target.NodeId)}}</dd>
              <dt>Type</dt><dd>{{Html(target.NodeType)}}</dd>
              <dt>Name</dt><dd>{{Html(target.Name ?? "-")}}</dd>
              <dt>Text</dt><dd>{{Html(target.Text ?? "-")}}</dd>
              <dt>Bounds</dt><dd>{{Html(bounds)}}</dd>
              <dt>Classes</dt><dd>{{Html(classes)}}</dd>
            </dl>
            """;
    }

    private static string CreateDiagnosticsHtml(IEnumerable<ProtocolError> diagnostics)
    {
        var items = diagnostics.ToArray();
        if (items.Length == 0)
        {
            return "<p class=\"empty\">No diagnostics were reported.</p>";
        }

        return string.Join(Environment.NewLine, items.Select(diagnostic =>
        {
            var details = diagnostic.Details is null || diagnostic.Details.Count == 0
                ? string.Empty
                : "<pre>" + Html(JsonSerializer.Serialize(diagnostic.Details, JsonOptions)) + "</pre>";
            return $$"""
                <div class="entry">
                  <strong>{{Html(diagnostic.Code)}}</strong>
                  <span>{{Html(diagnostic.Message)}}</span>
                  {{details}}
                </div>
                """;
        }));
    }

    private static string CreateEntryRows(IReadOnlyList<RuntimeMutationReviewEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "<p class=\"empty\">No mutations are available.</p>";
        }

        return string.Join(Environment.NewLine, entries.Select(entry =>
        {
            var activeBadge = entry.Active ? "<span class=\"badge\">active</span>" : string.Empty;
            return $$"""
                <div class="entry">
                  {{activeBadge}}
                  <strong>{{Html(entry.MutationId)}}</strong>
                  <span>{{Html(entry.Operation.Kind)}} / {{Html(entry.Status)}} / target {{Html(entry.Target.NodeId ?? entry.Target.TopLevelId)}}</span>
                  <span class="empty">{{Html(entry.EvaluatedAt.ToString("O", CultureInfo.InvariantCulture))}}</span>
                </div>
                """;
        }));
    }

    private static string CreateSourceSuggestionRows(IReadOnlyList<RuntimeSourceSuggestion> suggestions)
    {
        if (suggestions.Count == 0)
        {
            return "<p class=\"empty\">No source suggestions are available for this review.</p>";
        }

        return string.Join(Environment.NewLine, suggestions.Select(suggestion =>
        {
            var file = suggestion.SuggestedFilePath is null
                ? "source file unknown"
                : suggestion.SuggestedFilePath;
            var limitations = string.Join(" ", suggestion.Limitations);
            return $$"""
                <div class="entry">
                  <span class="badge">{{Html(suggestion.Confidence)}}</span>
                  <strong>{{Html(suggestion.SuggestedTargetKind)}}</strong>
                  <span>{{Html(suggestion.SuggestedAction)}}</span>
                  <span class="empty">{{Html(file)}} / {{Html(suggestion.Provenance)}} / {{Html(suggestion.SourceFileStatus)}}</span>
                  <span class="empty">{{Html(limitations)}}</span>
                </div>
                """;
        }));
    }

    private static CoreResult<RuntimeMutationReviewArtifact> Unavailable(string message)
    {
        return CoreResult<RuntimeMutationReviewArtifact>.Fail(new CoreError(
            CoreErrorCodes.RuntimeMutationReviewUnavailable,
            message));
    }

    private static string Html(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
            .ToArray();
        var fileName = new string(chars).Trim('-', '.');
        return string.IsNullOrWhiteSpace(fileName) ? "mutation-review" : fileName;
    }
}
