using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class RuntimeMutationReviewExporter
{
    private const int MaximumNodeMapEntries = 512;
    private const int MaximumPropertyOriginsPerNode = 12;
    private const int MaximumBindingsPerNode = 12;
    private const int MaximumDiagnosticsPerNode = 8;

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
        var beforeFigure = CreateImageFigure("Before", evidence.BeforeScreenshotPath, "before");
        var afterFigure = CreateImageFigure("After", evidence.AfterScreenshotPath, "after");
        var diffFigure = CreateImageFigure("Diff", evidence.DiffPath);
        var beforeTarget = evidence.BeforeTarget is null
            ? "<p class=\"empty\">Before target was not found in the captured visual tree.</p>"
            : CreateTargetHtml(evidence.BeforeTarget);
        var afterTarget = evidence.AfterTarget is null
            ? "<p class=\"empty\">After target was not found in the captured visual tree.</p>"
            : CreateTargetHtml(evidence.AfterTarget);
        var nodeMaps = CreateEvidenceNodeMaps(evidence);
        var nodeMapPanel = CreateNodeMapPanel(nodeMaps);
        var nodeMapData = JsonSerializer.Serialize(nodeMaps, JsonOptions);
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
            {{nodeMapPanel}}
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
            <script type="application/json" id="avascope-node-map-data">{{Html(nodeMapData)}}</script>
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

                figure img[data-node-map-id] {
                  cursor: crosshair;
                }

                figure img.node-map-selected {
                  border-color: var(--accent);
                  box-shadow: 0 0 0 2px color-mix(in srgb, var(--accent) 60%, transparent);
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

                .node-map-result {
                  border: 1px solid var(--border);
                  border-radius: 8px;
                  display: grid;
                  gap: 8px;
                  padding: 12px;
                  background: #0a0e0c;
                }

                .node-map-properties {
                  display: grid;
                  gap: 6px;
                }

                .node-map-property {
                  border-top: 1px solid var(--border);
                  padding-top: 6px;
                }

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
              {{CreateNodeMapScript()}}
            </body>
            </html>
            """;
    }

    private static string CreateImageFigure(string label, string? path, string? nodeMapId = null)
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
        var nodeMapAttributes = string.IsNullOrWhiteSpace(nodeMapId)
            ? string.Empty
            : $" class=\"node-map-image\" data-node-map-id=\"{Html(nodeMapId)}\" title=\"Click to map this screenshot region to the nearest inspected node.\"";
        var image = File.Exists(fullPath)
            ? $"<img{nodeMapAttributes} alt=\"{Html(label)} screenshot\" src=\"{Html(new Uri(fullPath).AbsoluteUri)}\">"
            : "<p class=\"empty\">Artifact file was not found on disk.</p>";

        return $$"""
            <figure>
              <h2>{{Html(label)}}</h2>
              {{image}}
              <figcaption>{{Html(fullPath)}}</figcaption>
            </figure>
            """;
    }

    private static IReadOnlyDictionary<string, ScreenshotNodeMap> CreateEvidenceNodeMaps(
        RuntimeMutationEvidenceResponse evidence)
    {
        return new Dictionary<string, ScreenshotNodeMap>(StringComparer.Ordinal)
        {
            ["before"] = CreateScreenshotNodeMap("before", evidence.BeforeVisualTreePath),
            ["after"] = CreateScreenshotNodeMap("after", evidence.AfterVisualTreePath)
        };
    }

    private static ScreenshotNodeMap CreateScreenshotNodeMap(string stage, string? snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath))
        {
            return new ScreenshotNodeMap(stage, "unavailable", null, [], ["Visual tree snapshot path was not provided."]);
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(snapshotPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new ScreenshotNodeMap(stage, "unavailable", snapshotPath, [], [$"Visual tree snapshot path is invalid: {exception.Message}"]);
        }

        if (!File.Exists(fullPath))
        {
            return new ScreenshotNodeMap(stage, "unavailable", fullPath, [], ["Visual tree snapshot file was not found."]);
        }

        try
        {
            var tree = JsonSerializer.Deserialize<TreeResponse>(File.ReadAllText(fullPath), JsonOptions);
            if (tree is null)
            {
                return new ScreenshotNodeMap(stage, "unavailable", fullPath, [], ["Visual tree snapshot could not be read."]);
            }

            var nodes = new List<ScreenshotNodeMapEntry>();
            var truncated = CollectNodeMapEntries(tree.Root, nodes);
            IReadOnlyList<string> diagnostics = truncated
                ? [$"Node map was truncated at {MaximumNodeMapEntries.ToString(CultureInfo.InvariantCulture)} bounded nodes."]
                : [];
            var status = nodes.Count == 0 ? "not_available" : "available";
            return new ScreenshotNodeMap(stage, status, fullPath, nodes, diagnostics);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return new ScreenshotNodeMap(stage, "unavailable", fullPath, [], [$"Visual tree snapshot could not be read: {exception.Message}"]);
        }
    }

    private static bool CollectNodeMapEntries(TreeNodeSummary node, List<ScreenshotNodeMapEntry> entries)
    {
        if (entries.Count >= MaximumNodeMapEntries)
        {
            return true;
        }

        var bounds = node.Bounds;
        if (bounds is not null)
        {
            entries.Add(new ScreenshotNodeMapEntry(
                node.NodeId,
                node.NodeType,
                node.Name,
                node.AutomationId,
                node.Text,
                bounds,
                node.Classes,
                CreateNodeSourceMap(node.SourceMap)));

            if (entries.Count >= MaximumNodeMapEntries)
            {
                return true;
            }
        }

        foreach (var child in node.Children)
        {
            if (CollectNodeMapEntries(child, entries))
            {
                return true;
            }
        }

        return false;
    }

    private static ScreenshotNodeSourceMap? CreateNodeSourceMap(RuntimeNodeSourceMap? sourceMap)
    {
        if (sourceMap is null)
        {
            return null;
        }

        return new ScreenshotNodeSourceMap(
            sourceMap.Status,
            sourceMap.Provenance,
            sourceMap.FilePath,
            sourceMap.Line,
            sourceMap.Column,
            sourceMap.XName,
            sourceMap.ElementType,
            sourceMap.ElementPath,
            sourceMap.PropertyOrigins.Take(MaximumPropertyOriginsPerNode).ToArray(),
            sourceMap.Bindings.Take(MaximumBindingsPerNode).ToArray(),
            sourceMap.Diagnostics.Take(MaximumDiagnosticsPerNode).ToArray());
    }

    private static string CreateNodeMapPanel(IReadOnlyDictionary<string, ScreenshotNodeMap> nodeMaps)
    {
        var before = nodeMaps.TryGetValue("before", out var beforeMap) ? beforeMap : null;
        var after = nodeMaps.TryGetValue("after", out var afterMap) ? afterMap : null;

        return $$"""
            <section class="panel">
              <h2>Screenshot Node Map</h2>
              <p class="empty">Click the before or after screenshot to map the screenshot region to the nearest inspected node and provenance summary.</p>
              <dl class="facts">
                <dt>Before map</dt><dd>{{Html(DescribeNodeMap(before))}}</dd>
                <dt>After map</dt><dd>{{Html(DescribeNodeMap(after))}}</dd>
              </dl>
              <div id="node-map-result" class="node-map-result">
                <strong>No screenshot node selected.</strong>
                <span class="empty">Select a point on a mapped screenshot.</span>
              </div>
            </section>
            """;
    }

    private static string DescribeNodeMap(ScreenshotNodeMap? map)
    {
        if (map is null)
        {
            return "unavailable";
        }

        return $"{map.Status}, {map.Nodes.Count.ToString(CultureInfo.InvariantCulture)} bounded node(s)";
    }

    private static string CreateNodeMapScript()
    {
        return """
            <script>
            (() => {
              const dataElement = document.getElementById('avascope-node-map-data');
              const result = document.getElementById('node-map-result');
              if (!dataElement || !result) {
                return;
              }

              let nodeMaps = {};
              try {
                nodeMaps = JSON.parse(dataElement.textContent || '{}');
              } catch (error) {
                result.innerHTML = `<strong>Node map unavailable.</strong><span class="empty">${escapeHtml(error.message)}</span>`;
                return;
              }

              const images = Array.from(document.querySelectorAll('img[data-node-map-id]'));
              for (const image of images) {
                image.addEventListener('click', event => {
                  const mapId = image.dataset.nodeMapId;
                  const map = nodeMaps[mapId];
                  if (!map || !Array.isArray(map.nodes) || map.nodes.length === 0) {
                    renderUnavailable(mapId, map);
                    return;
                  }

                  const rect = image.getBoundingClientRect();
                  const scaleX = image.naturalWidth > 0 ? image.naturalWidth / rect.width : 1;
                  const scaleY = image.naturalHeight > 0 ? image.naturalHeight / rect.height : 1;
                  const x = (event.clientX - rect.left) * scaleX;
                  const y = (event.clientY - rect.top) * scaleY;
                  const node = pickNearestNode(map.nodes, x, y);
                  for (const other of images) {
                    other.classList.toggle('node-map-selected', other === image);
                  }

                  renderSelection(mapId, x, y, node);
                });
              }

              function pickNearestNode(nodes, x, y) {
                let contained = null;
                let containedArea = Number.POSITIVE_INFINITY;
                let nearest = null;
                let nearestScore = Number.POSITIVE_INFINITY;

                for (const node of nodes) {
                  const bounds = node.bounds;
                  if (!bounds) {
                    continue;
                  }

                  const left = Number(bounds.x);
                  const top = Number(bounds.y);
                  const width = Number(bounds.width);
                  const height = Number(bounds.height);
                  if (![left, top, width, height].every(Number.isFinite)) {
                    continue;
                  }

                  const right = left + width;
                  const bottom = top + height;
                  const minX = Math.min(left, right);
                  const maxX = Math.max(left, right);
                  const minY = Math.min(top, bottom);
                  const maxY = Math.max(top, bottom);
                  const area = Math.max(Math.abs(width), 1) * Math.max(Math.abs(height), 1);
                  const inside = x >= minX && x <= maxX && y >= minY && y <= maxY;
                  if (inside && area < containedArea) {
                    contained = node;
                    containedArea = area;
                  }

                  const dx = x < minX ? minX - x : x > maxX ? x - maxX : 0;
                  const dy = y < minY ? minY - y : y > maxY ? y - maxY : 0;
                  const score = (dx * dx) + (dy * dy) + (area * 0.000001);
                  if (score < nearestScore) {
                    nearest = node;
                    nearestScore = score;
                  }
                }

                return contained || nearest;
              }

              function renderUnavailable(mapId, map) {
                const diagnostics = Array.isArray(map?.diagnostics) ? map.diagnostics : ['No bounded nodes were available.'];
                result.innerHTML = `
                  <strong>${escapeHtml(labelFor(mapId))} node map unavailable.</strong>
                  <span class="empty">${escapeHtml(diagnostics.join(' '))}</span>`;
              }

              function renderSelection(mapId, x, y, node) {
                if (!node) {
                  result.innerHTML = `
                    <strong>No inspected node matched this point.</strong>
                    <span class="empty">${escapeHtml(labelFor(mapId))} @ ${formatNumber(x)}, ${formatNumber(y)}</span>`;
                  return;
                }

                const source = node.sourceMap || {};
                const fileLine = source.filePath
                  ? `${source.filePath}${source.line ? ':' + source.line : ''}`
                  : 'unknown source';
                const origins = Array.isArray(source.propertyOrigins) ? source.propertyOrigins : [];
                const bindings = Array.isArray(source.bindings) ? source.bindings : [];
                const propertyHtml = origins.length === 0
                  ? '<span class="empty">No property provenance was available for this node.</span>'
                  : origins.map(origin => `
                      <div class="node-map-property">
                        <strong>${escapeHtml(origin.propertyName)}</strong>
                        <span>${escapeHtml(origin.value)} (${escapeHtml(origin.valueType)})</span>
                        <span class="empty">${escapeHtml(origin.origin)} / ${escapeHtml(origin.priority)}${origin.resourceKey ? ' / resource ' + escapeHtml(origin.resourceKey) : ''}${origin.styleSelector ? ' / selector ' + escapeHtml(origin.styleSelector) : ''}</span>
                      </div>`).join('');
                const bindingHtml = bindings.length === 0
                  ? '<span class="empty">No binding path metadata was available.</span>'
                  : bindings.map(binding => `
                      <div class="node-map-property">
                        <strong>${escapeHtml(binding.targetProperty)}</strong>
                        <span>${escapeHtml(binding.bindingPath)} / ${escapeHtml(binding.bindingKind)} / ${escapeHtml(binding.status)}</span>
                      </div>`).join('');

                result.innerHTML = `
                  <strong>${escapeHtml(labelFor(mapId))}: ${escapeHtml(node.nodeId)} (${escapeHtml(node.nodeType)})</strong>
                  <span>${escapeHtml(node.name || source.xName || '-')} ${node.text ? '/ ' + escapeHtml(node.text) : ''}</span>
                  <span class="empty">clicked ${formatNumber(x)}, ${formatNumber(y)}; bounds ${formatBounds(node.bounds)}</span>
                  <span class="empty">source ${escapeHtml(fileLine)} / ${escapeHtml(source.status || 'unknown')} / ${escapeHtml(source.provenance || 'unknown')}</span>
                  <div class="node-map-properties">${propertyHtml}</div>
                  <div class="node-map-properties">${bindingHtml}</div>`;
              }

              function formatBounds(bounds) {
                if (!bounds) {
                  return 'unknown';
                }

                return `${formatNumber(bounds.x)}, ${formatNumber(bounds.y)}, ${formatNumber(bounds.width)} x ${formatNumber(bounds.height)}`;
              }

              function formatNumber(value) {
                const number = Number(value);
                return Number.isFinite(number) ? number.toFixed(1).replace(/\.0$/, '') : 'unknown';
              }

              function labelFor(mapId) {
                return mapId === 'before' ? 'Before screenshot' : mapId === 'after' ? 'After screenshot' : String(mapId || 'Screenshot');
              }

              function escapeHtml(value) {
                return String(value ?? '').replace(/[&<>"']/g, character => ({
                  '&': '&amp;',
                  '<': '&lt;',
                  '>': '&gt;',
                  '"': '&quot;',
                  "'": '&#39;'
                }[character]));
              }
            })();
            </script>
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

    private sealed record ScreenshotNodeMap(
        string Stage,
        string Status,
        string? SnapshotPath,
        IReadOnlyList<ScreenshotNodeMapEntry> Nodes,
        IReadOnlyList<string> Diagnostics);

    private sealed record ScreenshotNodeMapEntry(
        string NodeId,
        string NodeType,
        string? Name,
        string? AutomationId,
        string? Text,
        NodeBounds Bounds,
        IReadOnlyList<string> Classes,
        ScreenshotNodeSourceMap? SourceMap);

    private sealed record ScreenshotNodeSourceMap(
        string Status,
        string Provenance,
        string? FilePath,
        int? Line,
        int? Column,
        string? XName,
        string? ElementType,
        string? ElementPath,
        IReadOnlyList<RuntimeSourcePropertyOrigin> PropertyOrigins,
        IReadOnlyList<RuntimeSourceBinding> Bindings,
        IReadOnlyList<ProtocolError> Diagnostics);

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
