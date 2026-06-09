using System.Net;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewAnimationViewerExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly TimeProvider _timeProvider;

    public PreviewAnimationViewerExporter()
        : this(TimeProvider.System)
    {
    }

    public PreviewAnimationViewerExporter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public CoreResult<PreviewAnimationViewerResponse> Export(
        PreviewAnimationResponse animation,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(animation);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return CoreResult<PreviewAnimationViewerResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewViewerUnavailable,
                "Animation viewer output path cannot be empty."));
        }

        var frames = animation.Frames
            .Where(static frame => frame.Render.Success
                && frame.Render.Value is not null
                && File.Exists(frame.Render.Value.FilePath))
            .ToArray();
        if (frames.Length == 0)
        {
            return CoreResult<PreviewAnimationViewerResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewViewerUnavailable,
                "Animation viewer requires at least one successful frame image."));
        }

        string viewerPath;
        try
        {
            viewerPath = Path.GetFullPath(outputPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CoreResult<PreviewAnimationViewerResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewViewerUnavailable,
                $"Animation viewer path is invalid: {exception.Message}"));
        }

        try
        {
            var directory = Path.GetDirectoryName(viewerPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var html = CreateHtml(animation, frames);
            File.WriteAllText(viewerPath, html, Encoding.UTF8);
            return CoreResult<PreviewAnimationViewerResponse>.Ok(new PreviewAnimationViewerResponse(
                viewerPath,
                new Uri(viewerPath).AbsoluteUri,
                _timeProvider.GetUtcNow()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return CoreResult<PreviewAnimationViewerResponse>.Fail(new CoreError(
                CoreErrorCodes.PreviewViewerUnavailable,
                $"Animation viewer could not be written: {exception.Message}"));
        }
    }

    private static string CreateHtml(
        PreviewAnimationResponse animation,
        IReadOnlyList<PreviewAnimationFrame> frames)
    {
        var firstRender = frames[0].Render.Value!;
        var title = firstRender.ViewPath ?? Path.GetFileName(firstRender.FilePath);
        var frameHtml = string.Join(
            Environment.NewLine,
            frames.Select(CreateFrameHtml));
        var stripHtml = string.IsNullOrWhiteSpace(animation.FrameStripPath) || !File.Exists(animation.FrameStripPath)
            ? "<p class=\"empty\">No frame strip was generated.</p>"
            : $$"""
              <img class="strip" alt="Animation frame strip" src="data:image/png;base64,{{ReadImageData(animation.FrameStripPath)}}">
              <p class="path">{{Html(animation.FrameStripPath)}}</p>
              """;
        var diagnostics = animation.Diagnostics.Count == 0
            ? "<p class=\"empty\">No animation diagnostics were reported.</p>"
            : string.Join(
                Environment.NewLine,
                animation.Diagnostics.Select(CreateDiagnosticHtml));
        var animationJson = JsonSerializer.Serialize(animation, JsonOptions);

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>AvaScope Animation - {{Html(title)}}</title>
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
                  grid-template-columns: minmax(0, 1fr) minmax(320px, 430px);
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

                .timeline {
                  overflow: auto;
                  padding: 16px;
                }

                .frames {
                  display: grid;
                  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
                  gap: 12px;
                }

                figure {
                  margin: 0;
                  border: 1px solid var(--border);
                  border-radius: 6px;
                  overflow: hidden;
                  background: #0d1117;
                }

                figure img,
                .strip {
                  display: block;
                  max-width: 100%;
                  height: auto;
                  background: #fff;
                }

                figcaption {
                  padding: 8px 10px;
                  color: var(--muted);
                  overflow-wrap: anywhere;
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
                  grid-template-columns: 130px minmax(0, 1fr);
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

                .empty,
                .path {
                  color: var(--muted);
                  overflow-wrap: anywhere;
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
                <section class="timeline" aria-label="Animation frames">
                  <div class="frames">
                    {{frameHtml}}
                  </div>
                  <h2>Frame Strip</h2>
                  {{stripHtml}}
                </section>
                <section class="details" aria-label="Animation details">
                  <h1>{{Html(title)}}</h1>
                  <dl>
                    <dt>Status</dt>
                    <dd>{{Html(animation.Motion.Status)}}</dd>
                    <dt>Frames</dt>
                    <dd>{{animation.Motion.ComparedFrameCount}}</dd>
                    <dt>Changed Pixels</dt>
                    <dd>{{animation.Motion.ChangedPixels}}</dd>
                    <dt>Changed Percent</dt>
                    <dd>{{animation.Motion.ChangedPercent.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}}%</dd>
                    <dt>Max Delta</dt>
                    <dd>{{animation.Motion.MaxDelta}}</dd>
                    <dt>Sampled</dt>
                    <dd>{{Html(animation.SampledAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture))}}</dd>
                    <dt>Project</dt>
                    <dd>{{Html(firstRender.ProjectPath ?? "-")}}</dd>
                    <dt>View</dt>
                    <dd>{{Html(firstRender.ViewPath ?? "-")}}</dd>
                  </dl>
                  <h2>Diagnostics</h2>
                  {{diagnostics}}
                  <h2>Animation JSON</h2>
                  <pre id="animation-json">{{Html(animationJson)}}</pre>
                </section>
              </main>
            </body>
            </html>
            """;
    }

    private static string CreateFrameHtml(PreviewAnimationFrame frame)
    {
        var render = frame.Render.Value!;
        return $$"""
            <figure>
              <img alt="Animation frame at {{frame.TimeOffsetMs}}ms" src="data:image/png;base64,{{ReadImageData(render.FilePath)}}">
              <figcaption>{{frame.TimeOffsetMs}}ms<br>{{Html(render.FilePath)}}</figcaption>
            </figure>
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

    private static string ReadImageData(string imagePath)
    {
        return Convert.ToBase64String(File.ReadAllBytes(imagePath));
    }

    private static string Html(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
