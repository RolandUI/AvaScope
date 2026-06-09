using System.Net;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewViewerExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly TimeProvider _timeProvider;

    public PreviewViewerExporter()
        : this(TimeProvider.System)
    {
    }

    public PreviewViewerExporter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public CoreResult<PreviewViewerResponse> Export(
        PreviewSessionSummary session,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!session.LastRender.Success || session.LastRender.Value is null)
        {
            return CoreResult<PreviewViewerResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewViewerUnavailable,
                $"Preview session '{session.Session.SessionId}' does not have a successful render to display."));
        }

        var render = session.LastRender.Value;
        var imagePath = Path.GetFullPath(render.FilePath);
        if (!File.Exists(imagePath))
        {
            return CoreResult<PreviewViewerResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewViewerUnavailable,
                $"Preview image '{imagePath}' was not found."));
        }

        string viewerPath;
        try
        {
            viewerPath = string.IsNullOrWhiteSpace(outputPath)
                ? CreateDefaultViewerPath(session, imagePath)
                : Path.GetFullPath(outputPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CoreResult<PreviewViewerResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewViewerUnavailable,
                $"Preview viewer path is invalid: {exception.Message}"));
        }

        try
        {
            var directory = Path.GetDirectoryName(viewerPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var html = CreateHtml(session, render, imagePath);
            File.WriteAllText(viewerPath, html, Encoding.UTF8);
            return CoreResult<PreviewViewerResponse>.Ok(new PreviewViewerResponse(
                session,
                viewerPath,
                new Uri(viewerPath).AbsoluteUri,
                _timeProvider.GetUtcNow()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return CoreResult<PreviewViewerResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewViewerUnavailable,
                $"Preview viewer could not be written: {exception.Message}"));
        }
    }

    private static string CreateDefaultViewerPath(PreviewSessionSummary session, string imagePath)
    {
        var directory = Path.GetDirectoryName(imagePath) ?? Environment.CurrentDirectory;
        var fileName = $"{SanitizeFileName(session.Session.SessionId.Value)}.avascope-preview.html";
        return Path.Combine(directory, fileName);
    }

    private static string CreateHtml(
        PreviewSessionSummary session,
        PreviewResponse render,
        string imagePath)
    {
        var imageData = Convert.ToBase64String(File.ReadAllBytes(imagePath));
        var sessionJson = JsonSerializer.Serialize(session, JsonOptions);
        var title = session.Session.DisplayName ?? session.Request.ViewPath ?? session.Session.SessionId.Value;
        var diagnostics = render.Diagnostics.Count == 0
            ? "<p class=\"empty\">No preview diagnostics were reported.</p>"
            : string.Join(
                Environment.NewLine,
                render.Diagnostics.Select(CreateDiagnosticHtml));

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>AvaScope Preview - {{Html(title)}}</title>
              <style>
                :root {
                  color-scheme: light dark;
                  --bg: #111418;
                  --panel: #1a2027;
                  --text: #eef3f8;
                  --muted: #9da8b5;
                  --border: #34404c;
                  --accent: #5cc8ff;
                }

                * {
                  box-sizing: border-box;
                }

                body {
                  margin: 0;
                  font-family: "Segoe UI", system-ui, sans-serif;
                  background: var(--bg);
                  color: var(--text);
                }

                main {
                  display: grid;
                  grid-template-columns: minmax(0, 1fr) minmax(320px, 420px);
                  gap: 16px;
                  min-height: 100vh;
                  padding: 16px;
                }

                section {
                  border: 1px solid var(--border);
                  border-radius: 8px;
                  background: var(--panel);
                  min-width: 0;
                }

                .preview {
                  display: flex;
                  align-items: center;
                  justify-content: center;
                  overflow: auto;
                  padding: 16px;
                }

                .preview img {
                  max-width: 100%;
                  height: auto;
                  image-rendering: auto;
                  background: #fff;
                  border: 1px solid var(--border);
                }

                .details {
                  padding: 16px;
                  overflow: auto;
                }

                h1 {
                  font-size: 18px;
                  margin: 0 0 12px;
                }

                h2 {
                  font-size: 14px;
                  margin: 20px 0 8px;
                  color: var(--accent);
                }

                dl {
                  display: grid;
                  grid-template-columns: 110px minmax(0, 1fr);
                  gap: 8px 12px;
                  margin: 0;
                }

                dt {
                  color: var(--muted);
                }

                dd {
                  margin: 0;
                  overflow-wrap: anywhere;
                }

                .diagnostic {
                  border-top: 1px solid var(--border);
                  padding: 10px 0;
                }

                .diagnostic:first-of-type {
                  border-top: 0;
                }

                .diagnostic strong {
                  display: block;
                  margin-bottom: 4px;
                }

                .empty {
                  color: var(--muted);
                }

                pre {
                  white-space: pre-wrap;
                  overflow-wrap: anywhere;
                  border: 1px solid var(--border);
                  border-radius: 6px;
                  padding: 12px;
                  background: #0d1117;
                  color: #d7dee7;
                }

                @media (max-width: 900px) {
                  main {
                    grid-template-columns: 1fr;
                  }
                }
              </style>
            </head>
            <body>
              <main>
                <section class="preview" aria-label="Preview image">
                  <img alt="AvaScope preview screenshot" src="data:image/png;base64,{{imageData}}">
                </section>
                <section class="details" aria-label="Preview details">
                  <h1>{{Html(title)}}</h1>
                  <dl>
                    <dt>Session</dt>
                    <dd>{{Html(session.Session.SessionId.Value)}}</dd>
                    <dt>State</dt>
                    <dd>{{Html(session.Session.State)}}</dd>
                    <dt>Image</dt>
                    <dd>{{Html(imagePath)}}</dd>
                    <dt>Size</dt>
                    <dd>{{render.PixelWidth}} x {{render.PixelHeight}} @ {{render.Dpi.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}} DPI</dd>
                    <dt>Rendered</dt>
                    <dd>{{Html(render.RenderedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture))}}</dd>
                    <dt>Project</dt>
                    <dd>{{Html(render.ProjectPath ?? "-")}}</dd>
                    <dt>View</dt>
                    <dd>{{Html(render.ViewPath ?? "-")}}</dd>
                    <dt>Theme</dt>
                    <dd>{{Html(render.ThemeVariant ?? "-")}}</dd>
                    <dt>Culture</dt>
                    <dd>{{Html(render.Culture ?? "-")}}</dd>
                  </dl>
                  <h2>Diagnostics</h2>
                  {{diagnostics}}
                  <h2>Session JSON</h2>
                  <pre id="session-json">{{Html(sessionJson)}}</pre>
                </section>
              </main>
            </body>
            </html>
            """;
    }

    private static string CreateDiagnosticHtml(PreviewDiagnostic diagnostic)
    {
        var heading = $"{diagnostic.Severity} / {diagnostic.Category} / {diagnostic.Code}";
        var detail = diagnostic.Details.Count == 0
            ? string.Empty
            : "<pre>" + Html(JsonSerializer.Serialize(diagnostic.Details, JsonOptions)) + "</pre>";
        return $$"""
            <div class="diagnostic">
              <strong>{{Html(heading)}}</strong>
              <div>{{Html(diagnostic.Message)}}</div>
              <div class="empty">{{Html(diagnostic.NodeId ?? diagnostic.SourcePath ?? string.Empty)}}</div>
              {{detail}}
            </div>
            """;
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
        var fileName = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(fileName) ? "preview" : fileName;
    }
}
