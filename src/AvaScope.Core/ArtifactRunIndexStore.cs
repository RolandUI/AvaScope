using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class ArtifactRunIndexStore
{
    public const string DirectoryEnvironmentVariable = "AVASCOPE_RUN_INDEX_DIR";
    private const string RootDirectoryName = "AvaScope";
    private const string RunIndexDirectoryName = "run-indexes";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly TimeProvider _timeProvider;

    public ArtifactRunIndexStore(string directory)
        : this(directory, TimeProvider.System)
    {
    }

    public ArtifactRunIndexStore(string directory, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Artifact run index directory cannot be empty.", nameof(directory));
        }

        Directory = Path.GetFullPath(directory);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public string Directory { get; }

    public static ArtifactRunIndexStore CreateDefault()
    {
        return new ArtifactRunIndexStore(GetDefaultDirectory());
    }

    public static string GetDefaultDirectory()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(Path.GetTempPath(), RootDirectoryName, RunIndexDirectoryName)
            : configuredDirectory;
    }

    public CoreResult<ArtifactRunIndexResponse> Write(ArtifactRunIndexRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var generatedAt = _timeProvider.GetUtcNow();
            var taskKey = CreateTaskKey(request);
            var runId = CreateRunId(generatedAt);
            var runDirectory = Path.Combine(Directory, taskKey, "runs", runId);
            var indexJsonPath = Path.Combine(runDirectory, "run-index.json");
            var indexHtmlPath = Path.Combine(runDirectory, "run-index.html");
            var latestPointerPath = Path.Combine(Directory, taskKey, "latest-run.json");
            var screenshotPaths = request.Artifacts
                .Where(IsImageLikeArtifact)
                .Select(static artifact => artifact.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var response = new ArtifactRunIndexResponse(
                runId,
                taskKey,
                request.Command,
                request.Status,
                generatedAt,
                indexJsonPath,
                indexHtmlPath,
                latestPointerPath,
                request.TaskName,
                request.RunGroup,
                request.ProjectPath,
                request.ViewPath,
                request.Profile,
                request.Variant,
                request.StateVariant,
                screenshotPaths,
                request.Artifacts,
                request.Diagnostics,
                request.Warnings,
                request.GeneratedReports,
                request.Metadata,
                request.StartedAt,
                request.CompletedAt);
            var pointer = new ArtifactLatestRunPointer(
                taskKey,
                runId,
                indexJsonPath,
                indexHtmlPath,
                generatedAt,
                request.Command,
                request.TaskName,
                request.RunGroup,
                request.ProjectPath,
                request.ViewPath,
                request.Profile,
                request.Variant,
                request.StateVariant);

            System.IO.Directory.CreateDirectory(runDirectory);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(latestPointerPath)!);
            File.WriteAllText(indexJsonPath, JsonSerializer.Serialize(response, JsonOptions), Encoding.UTF8);
            File.WriteAllText(indexHtmlPath, CreateHtml(response), Encoding.UTF8);
            File.WriteAllText(latestPointerPath, JsonSerializer.Serialize(pointer, JsonOptions), Encoding.UTF8);

            return CoreResult<ArtifactRunIndexResponse>.Ok(response);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Unavailable($"Artifact run index could not be written: {exception.Message}");
        }
    }

    public CoreResult<ArtifactRunIndexResponse> ResolveLatest(ArtifactRunIndexSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        try
        {
            var taskKey = CreateTaskKey(selector);
            var pointerPath = Path.Combine(Directory, taskKey, "latest-run.json");
            if (!File.Exists(pointerPath))
            {
                return Unavailable(
                    "Latest run pointer was not found.",
                    new Dictionary<string, string>
                    {
                        ["taskKey"] = taskKey,
                        ["latestPointerPath"] = pointerPath
                    });
            }

            var pointer = JsonSerializer.Deserialize<ArtifactLatestRunPointer>(
                File.ReadAllText(pointerPath, Encoding.UTF8),
                JsonOptions);
            if (pointer is null)
            {
                return Unavailable(
                    "Latest run pointer did not contain a valid JSON object.",
                    new Dictionary<string, string> { ["latestPointerPath"] = pointerPath });
            }

            if (!File.Exists(pointer.IndexJsonPath))
            {
                return Unavailable(
                    "Latest run index JSON was not found.",
                    new Dictionary<string, string>
                    {
                        ["latestPointerPath"] = pointerPath,
                        ["indexJsonPath"] = pointer.IndexJsonPath
                    });
            }

            var response = JsonSerializer.Deserialize<ArtifactRunIndexResponse>(
                File.ReadAllText(pointer.IndexJsonPath, Encoding.UTF8),
                JsonOptions);
            return response is null
                ? Unavailable(
                    "Latest run index JSON did not contain a valid JSON object.",
                    new Dictionary<string, string> { ["indexJsonPath"] = pointer.IndexJsonPath })
                : CoreResult<ArtifactRunIndexResponse>.Ok(response);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Unavailable($"Latest run pointer could not be resolved: {exception.Message}");
        }
    }

    public static string CreateTaskKey(ArtifactRunIndexRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CreateTaskKey(
            request.TaskName,
            request.RunGroup,
            request.ProjectPath,
            request.ViewPath,
            request.Profile,
            request.Variant,
            request.StateVariant,
            request.Command);
    }

    public static string CreateTaskKey(ArtifactRunIndexSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return CreateTaskKey(
            selector.TaskName,
            selector.RunGroup,
            selector.ProjectPath,
            selector.ViewPath,
            selector.Profile,
            selector.Variant,
            selector.StateVariant,
            selector.Command);
    }

    private static string CreateTaskKey(
        string? taskName,
        string? runGroup,
        string? projectPath,
        string? viewPath,
        string? profile,
        string? variant,
        string? stateVariant,
        string? command)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(taskName))
        {
            parts.Add($"task:{taskName}");
        }
        else
        {
            AddPart(parts, "group", runGroup);
            AddPart(parts, "project", projectPath);
            AddPart(parts, "view", viewPath);
            AddPart(parts, "profile", profile);
            AddPart(parts, "variant", variant);
            AddPart(parts, "state", stateVariant);

            if (parts.Count == 0)
            {
                AddPart(parts, "command", command);
            }
        }

        var keySource = parts.Count == 0 ? "run" : string.Join("|", parts);
        var slug = CreateSlug(keySource);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keySource)))
            .ToLowerInvariant()[..10];
        return $"{slug}-{hash}";
    }

    private static void AddPart(ICollection<string> parts, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{name}:{value.Trim()}");
        }
    }

    private static string CreateRunId(DateTimeOffset generatedAt)
    {
        var stamp = generatedAt.UtcDateTime.ToString("yyyyMMddTHHmmssfffffffZ", CultureInfo.InvariantCulture);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return $"{stamp}-{suffix}";
    }

    private static string CreateSlug(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousDash = false;
        foreach (var character in value.ToLowerInvariant())
        {
            var next = char.IsLetterOrDigit(character) ? character : '-';
            if (next == '-' && previousDash)
            {
                continue;
            }

            builder.Append(next);
            previousDash = next == '-';
            if (builder.Length >= 80)
            {
                break;
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "run" : slug;
    }

    private static bool IsImageLikeArtifact(ArtifactRunIndexArtifact artifact)
    {
        return artifact.Kind.Contains("screenshot", StringComparison.OrdinalIgnoreCase)
            || artifact.Kind.Contains("preview", StringComparison.OrdinalIgnoreCase)
            || artifact.Kind.Contains("contact", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(artifact.Path), ".png", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateHtml(ArtifactRunIndexResponse response)
    {
        var facts = new Dictionary<string, string?>
        {
            ["Run"] = response.RunId,
            ["Task"] = response.TaskName,
            ["Task key"] = response.TaskKey,
            ["Group"] = response.RunGroup,
            ["Command"] = response.Command,
            ["Status"] = response.Status,
            ["Project"] = response.ProjectPath,
            ["View"] = response.ViewPath,
            ["Profile"] = response.Profile,
            ["Variant"] = response.Variant,
            ["State"] = response.StateVariant,
            ["Generated"] = response.GeneratedAt.ToString("O", CultureInfo.InvariantCulture)
        };
        var artifacts = response.Artifacts.Count == 0
            ? "<p class=\"empty\">No artifacts were recorded.</p>"
            : CreateArtifactList(response.Artifacts);
        var reports = response.GeneratedReports.Count == 0
            ? "<p class=\"empty\">No generated reports were recorded.</p>"
            : CreateArtifactList(response.GeneratedReports);
        var diagnostics = response.Diagnostics.Count == 0
            ? "<p class=\"empty\">No diagnostics were recorded.</p>"
            : string.Join(Environment.NewLine, response.Diagnostics.Select(CreateDiagnosticHtml));
        var warnings = response.Warnings.Count == 0
            ? "<p class=\"empty\">No warnings were recorded.</p>"
            : "<ul>" + string.Join(Environment.NewLine, response.Warnings.Select(static warning => $"<li>{Html(warning)}</li>")) + "</ul>";

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>AvaScope Run Index - {{Html(response.Command)}}</title>
              <style>
                :root {
                  color-scheme: light dark;
                  --bg: #111418;
                  --panel: #1b2229;
                  --text: #edf2f7;
                  --muted: #9aa8b6;
                  --border: #35414e;
                  --accent: #75d1ff;
                }

                * { box-sizing: border-box; }

                body {
                  margin: 0;
                  font-family: "Segoe UI", system-ui, sans-serif;
                  background: var(--bg);
                  color: var(--text);
                }

                main {
                  display: grid;
                  gap: 16px;
                  max-width: 1180px;
                  margin: 0 auto;
                  padding: 18px;
                }

                section {
                  border: 1px solid var(--border);
                  border-radius: 8px;
                  background: var(--panel);
                  padding: 16px;
                  min-width: 0;
                }

                h1 {
                  font-size: 22px;
                  margin: 0 0 12px;
                }

                h2 {
                  color: var(--accent);
                  font-size: 15px;
                  margin: 0 0 10px;
                }

                dl {
                  display: grid;
                  grid-template-columns: 140px minmax(0, 1fr);
                  gap: 8px 12px;
                  margin: 0;
                }

                dt { color: var(--muted); }
                dd { margin: 0; overflow-wrap: anywhere; }
                a { color: var(--accent); overflow-wrap: anywhere; }
                .empty { color: var(--muted); }
                .diagnostic {
                  border-top: 1px solid var(--border);
                  padding: 10px 0;
                }
                .diagnostic:first-child { border-top: 0; }
              </style>
            </head>
            <body>
              <main>
                <section>
                  <h1>AvaScope Run Index</h1>
                  {{CreateFacts(facts)}}
                </section>
                <section>
                  <h2>Artifacts</h2>
                  {{artifacts}}
                </section>
                <section>
                  <h2>Generated Reports</h2>
                  {{reports}}
                </section>
                <section>
                  <h2>Warnings</h2>
                  {{warnings}}
                </section>
                <section>
                  <h2>Diagnostics</h2>
                  {{diagnostics}}
                </section>
              </main>
            </body>
            </html>
            """;
    }

    private static string CreateFacts(IReadOnlyDictionary<string, string?> facts)
    {
        var rows = facts
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(static pair => $"<dt>{Html(pair.Key)}</dt><dd>{Html(pair.Value!)}</dd>");
        return "<dl>" + string.Join(Environment.NewLine, rows) + "</dl>";
    }

    private static string CreateArtifactList(IReadOnlyList<ArtifactRunIndexArtifact> artifacts)
    {
        var rows = artifacts.Select(static artifact =>
            $"<li><strong>{Html(artifact.Kind)}</strong> <a href=\"{Html(artifact.Url)}\">{Html(artifact.Path)}</a>{CreateDescription(artifact.Description)}</li>");
        return "<ul>" + string.Join(Environment.NewLine, rows) + "</ul>";
    }

    private static string CreateDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : $" <span class=\"empty\">{Html(description)}</span>";
    }

    private static string CreateDiagnosticHtml(ArtifactRunIndexDiagnostic diagnostic)
    {
        return $$"""
            <div class="diagnostic">
              <strong>{{Html(diagnostic.Severity)}} / {{Html(diagnostic.Category)}} / {{Html(diagnostic.Code)}}</strong>
              <div>{{Html(diagnostic.Message)}}</div>
              <div class="empty">{{Html(diagnostic.SourcePath ?? diagnostic.NodeId ?? string.Empty)}}</div>
            </div>
            """;
    }

    private static string Html(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static CoreResult<ArtifactRunIndexResponse> Unavailable(
        string message,
        IReadOnlyDictionary<string, string>? details = null)
    {
        return CoreResult<ArtifactRunIndexResponse>.Fail(new CoreError(
            CoreErrorCodes.ArtifactRunIndexUnavailable,
            message,
            details));
    }
}
