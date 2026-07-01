using System.Globalization;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Core;

public sealed class SemanticScreenshotComparer
{
    private const string Passed = "passed";
    private const string DifferencesFound = "differences_found";
    private const int CropPadding = 8;

    public CoreResult<SemanticScreenshotComparisonResponse> Compare(SemanticScreenshotComparisonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var outputDirectory = ResolveOutputDirectory(request);
        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return CoreResult<SemanticScreenshotComparisonResponse>.Fail(new CoreError(
                CoreErrorCodes.ImageDiffFailed,
                $"Semantic screenshot output directory could not be created: {exception.Message}",
                new Dictionary<string, string> { ["outputDirectory"] = outputDirectory }));
        }

        var diffPath = request.DiffPath
            ?? Path.Combine(outputDirectory, $"{SanitizeFileToken(request.RequestId)}-raw-diff.png");
        var rawDiffResult = new PreviewImageDiffer().Compare(
            request.ReferencePath,
            request.CurrentPath,
            diffPath,
            request.Tolerance);
        if (!rawDiffResult.Success)
        {
            return CoreResult<SemanticScreenshotComparisonResponse>.Fail(rawDiffResult.Error!);
        }

        try
        {
            using var reference = SKBitmap.Decode(request.ReferencePath);
            using var current = SKBitmap.Decode(request.CurrentPath);
            if (reference is null)
            {
                return CoreResult<SemanticScreenshotComparisonResponse>.Fail(new CoreError(
                    CoreErrorCodes.ImageDiffFailed,
                    $"Reference image '{request.ReferencePath}' could not be decoded."));
            }

            if (current is null)
            {
                return CoreResult<SemanticScreenshotComparisonResponse>.Fail(new CoreError(
                    CoreErrorCodes.ImageDiffFailed,
                    $"Current image '{request.CurrentPath}' could not be decoded."));
            }

            if (reference.Width != current.Width || reference.Height != current.Height)
            {
                return CoreResult<SemanticScreenshotComparisonResponse>.Fail(new CoreError(
                    CoreErrorCodes.ImageDiffDimensionMismatch,
                    "Reference and current image dimensions differ.",
                    new Dictionary<string, string>
                    {
                        ["referenceWidth"] = reference.Width.ToString(CultureInfo.InvariantCulture),
                        ["referenceHeight"] = reference.Height.ToString(CultureInfo.InvariantCulture),
                        ["currentWidth"] = current.Width.ToString(CultureInfo.InvariantCulture),
                        ["currentHeight"] = current.Height.ToString(CultureInfo.InvariantCulture)
                    }));
            }

            var changed = CreateChangedMask(reference, current, request.Tolerance, out var maxDelta);
            var components = ExtractComponents(changed, reference, current, request.MinChangedPixels)
                .OrderByDescending(static component => component.ChangedPixels)
                .ThenBy(static component => component.Y)
                .ThenBy(static component => component.X)
                .ToArray();
            var rawRegions = CreateRawRegions(current, components, request, outputDirectory);

            var referenceContent = FindContentBounds(reference, request.Tolerance);
            var currentContent = FindContentBounds(current, request.Tolerance);
            var findings = CreateSemanticFindings(
                current,
                components,
                referenceContent,
                currentContent,
                reference,
                request,
                outputDirectory);

            var annotatedPath = request.AnnotatedPath
                ?? Path.Combine(outputDirectory, $"{SanitizeFileToken(request.RequestId)}-semantic-annotated.png");
            var annotation = TryWriteAnnotatedOverview(current, rawRegions, findings, annotatedPath);
            var diagnostics = new List<ProtocolError>();
            if (!annotation.Success)
            {
                diagnostics.Add(new ProtocolError(annotation.Error!.Code, annotation.Error.Message, annotation.Error.Details));
                annotatedPath = string.Empty;
            }

            var status = rawDiffResult.Value!.Passed && findings.Count == 0 && rawRegions.Count == 0
                ? Passed
                : DifferencesFound;

            return CoreResult<SemanticScreenshotComparisonResponse>.Ok(new SemanticScreenshotComparisonResponse(
                request.RequestId,
                request.ReferencePath,
                request.CurrentPath,
                status,
                DateTimeOffset.UtcNow,
                rawDiffResult.Value!,
                string.IsNullOrWhiteSpace(annotatedPath) ? null : annotatedPath,
                rawRegions,
                findings,
                diagnostics,
                new Dictionary<string, string>
                {
                    ["outputDirectory"] = outputDirectory,
                    ["rawChangedComponents"] = components.Length.ToString(CultureInfo.InvariantCulture),
                    ["rawChangedMaxDelta"] = maxDelta.ToString(CultureInfo.InvariantCulture),
                    ["semanticProvenance"] = "pixel_diff_connected_components,content_bounds_heuristics,line_band_heuristics"
                }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return CoreResult<SemanticScreenshotComparisonResponse>.Fail(new CoreError(
                CoreErrorCodes.ImageDiffFailed,
                exception.Message));
        }
    }

    private static IReadOnlyList<SemanticScreenshotRawRegion> CreateRawRegions(
        SKBitmap current,
        IReadOnlyList<ChangedComponent> components,
        SemanticScreenshotComparisonRequest request,
        string outputDirectory)
    {
        var regions = new List<SemanticScreenshotRawRegion>();
        foreach (var component in components.Take(request.MaxRawRegions))
        {
            var regionId = $"raw-{(regions.Count + 1).ToString("00", CultureInfo.InvariantCulture)}";
            var region = ToScreenshotRegion(component, regionId);
            var cropPath = Path.Combine(outputDirectory, $"{SanitizeFileToken(request.RequestId)}-{regionId}-crop.png");
            var annotatedCropPath = Path.Combine(outputDirectory, $"{SanitizeFileToken(request.RequestId)}-{regionId}-annotated.png");
            var padded = Pad(region, current.Width, current.Height);
            WriteCrop(current, padded, cropPath, regionId, annotate: false);
            WriteCrop(current, padded, annotatedCropPath, regionId, annotate: true);

            regions.Add(new SemanticScreenshotRawRegion(
                regionId,
                region,
                component.ChangedPixels,
                component.ChangedPixels / (double)(region.Width * region.Height) * 100,
                component.MaxDelta,
                cropPath,
                annotatedCropPath,
                new Dictionary<string, string> { ["provenance"] = "pixel_diff_connected_component" }));
        }

        return regions;
    }

    private static IReadOnlyList<SemanticScreenshotFinding> CreateSemanticFindings(
        SKBitmap current,
        IReadOnlyList<ChangedComponent> components,
        ImageBounds? referenceContent,
        ImageBounds? currentContent,
        SKBitmap reference,
        SemanticScreenshotComparisonRequest request,
        string outputDirectory)
    {
        var findings = new List<SemanticScreenshotFinding>();
        if (referenceContent is not null && currentContent is not null)
        {
            var centerDeltaX = currentContent.CenterX - referenceContent.CenterX;
            var centerDeltaY = currentContent.CenterY - referenceContent.CenterY;
            if (Math.Abs(centerDeltaX) > 1 || Math.Abs(centerDeltaY) > 1)
            {
                AddFinding(
                    current,
                    request,
                    outputDirectory,
                    findings,
                    SemanticScreenshotFindingKinds.CenterMismatch,
                    "warning",
                    0.72,
                    "content_bounds_heuristic",
                    $"Likely center mismatch: current content center moved by {FormatDelta(centerDeltaX)} x and {FormatDelta(centerDeltaY)} y pixels.",
                    Union(referenceContent, currentContent, current.Width, current.Height),
                    new Dictionary<string, string>
                    {
                        ["centerDeltaX"] = centerDeltaX.ToString("0.###", CultureInfo.InvariantCulture),
                        ["centerDeltaY"] = centerDeltaY.ToString("0.###", CultureInfo.InvariantCulture)
                    });
            }

            var edgeDeltas = CreateEdgeDeltas(referenceContent, currentContent);
            if (edgeDeltas.Any(static delta => Math.Abs(delta.Value) > 1))
            {
                AddFinding(
                    current,
                    request,
                    outputDirectory,
                    findings,
                    SemanticScreenshotFindingKinds.EdgeMismatch,
                    "warning",
                    0.68,
                    "content_bounds_heuristic",
                    "Likely edge mismatch: content bounds changed relative to the reference.",
                    Union(referenceContent, currentContent, current.Width, current.Height),
                    edgeDeltas.ToDictionary(static item => $"{item.Key}Delta", static item => item.Value.ToString("0.###", CultureInfo.InvariantCulture)));
            }

            var paddingDeltas = CreatePaddingDeltas(referenceContent, currentContent, current.Width, current.Height);
            if (paddingDeltas.Any(static delta => Math.Abs(delta.Value) > 1))
            {
                AddFinding(
                    current,
                    request,
                    outputDirectory,
                    findings,
                    SemanticScreenshotFindingKinds.PaddingDifference,
                    "warning",
                    0.7,
                    "content_bounds_heuristic",
                    "Likely padding difference: distance from content to image edges changed.",
                    Union(referenceContent, currentContent, current.Width, current.Height),
                    paddingDeltas.ToDictionary(static item => $"{item.Key}PaddingDelta", static item => item.Value.ToString("0.###", CultureInfo.InvariantCulture)));
            }
        }

        var borderCandidate = components.FirstOrDefault(component =>
            component.Width <= 3 && component.Height >= 8
            || component.Height <= 3 && component.Width >= 8
            || TouchesImageEdge(component, current.Width, current.Height));
        if (borderCandidate != default)
        {
            AddFinding(
                current,
                request,
                outputDirectory,
                findings,
                SemanticScreenshotFindingKinds.BorderOrSeamDifference,
                "warning",
                0.63,
                "edge_band_heuristic",
                "Likely border, bleed, or seam difference: changed pixels form a thin edge-aligned band.",
                ToScreenshotRegion(borderCandidate, "border-candidate"),
                new Dictionary<string, string>
                {
                    ["changedPixels"] = borderCandidate.ChangedPixels.ToString(CultureInfo.InvariantCulture),
                    ["componentWidth"] = borderCandidate.Width.ToString(CultureInfo.InvariantCulture),
                    ["componentHeight"] = borderCandidate.Height.ToString(CultureInfo.InvariantCulture)
                });
        }

        var referenceBands = CountContentBands(reference, request.Tolerance);
        var currentBands = CountContentBands(current, request.Tolerance);
        if (referenceBands != currentBands)
        {
            AddFinding(
                current,
                request,
                outputDirectory,
                findings,
                SemanticScreenshotFindingKinds.WrappingDifference,
                "info",
                0.55,
                "line_band_heuristic",
                $"Possible wrapping change: reference has {referenceBands} content band(s), current has {currentBands}.",
                currentContent is null
                    ? new ScreenshotRegion(0, 0, current.Width, current.Height, "wrapping")
                    : ToScreenshotRegion(currentContent, "wrapping"),
                new Dictionary<string, string>
                {
                    ["referenceBands"] = referenceBands.ToString(CultureInfo.InvariantCulture),
                    ["currentBands"] = currentBands.ToString(CultureInfo.InvariantCulture)
                });
        }

        return findings.Take(request.MaxFindings).ToArray();
    }

    private static void AddFinding(
        SKBitmap current,
        SemanticScreenshotComparisonRequest request,
        string outputDirectory,
        List<SemanticScreenshotFinding> findings,
        string kind,
        string severity,
        double confidence,
        string provenance,
        string message,
        ScreenshotRegion region,
        IReadOnlyDictionary<string, string> metrics)
    {
        if (findings.Count >= request.MaxFindings)
        {
            return;
        }

        var findingId = $"{kind}-{(findings.Count + 1).ToString("00", CultureInfo.InvariantCulture)}";
        var cropPath = Path.Combine(outputDirectory, $"{SanitizeFileToken(request.RequestId)}-{findingId}-crop.png");
        var annotatedCropPath = Path.Combine(outputDirectory, $"{SanitizeFileToken(request.RequestId)}-{findingId}-annotated.png");
        var padded = Pad(region, current.Width, current.Height);
        WriteCrop(current, padded, cropPath, findingId, annotate: false);
        WriteCrop(current, padded, annotatedCropPath, findingId, annotate: true);

        findings.Add(new SemanticScreenshotFinding(
            findingId,
            kind,
            severity,
            confidence,
            provenance,
            message,
            region,
            cropPath,
            annotatedCropPath,
            metrics));
    }

    private static CoreResult<bool> TryWriteAnnotatedOverview(
        SKBitmap current,
        IReadOnlyList<SemanticScreenshotRawRegion> rawRegions,
        IReadOnlyList<SemanticScreenshotFinding> findings,
        string annotatedPath)
    {
        try
        {
            using var annotated = new SKBitmap(current.Width, current.Height);
            using var canvas = new SKCanvas(annotated);
            canvas.DrawBitmap(current, 0, 0);
            using var rawPaint = new SKPaint { Color = SKColors.Red, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
            using var findingPaint = new SKPaint { Color = new SKColor(0, 120, 215), StrokeWidth = 3, Style = SKPaintStyle.Stroke, IsAntialias = true };
            using var labelPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            using var labelBackground = new SKPaint { Color = new SKColor(255, 255, 255, 225), Style = SKPaintStyle.Fill };
            using var font = new SKFont { Size = 13 };

            foreach (var region in rawRegions)
            {
                DrawLabeledRect(canvas, region.Region, region.RegionId, rawPaint, labelPaint, labelBackground, font);
            }

            foreach (var finding in findings)
            {
                DrawLabeledRect(canvas, finding.Region, finding.Kind, findingPaint, labelPaint, labelBackground, font);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(annotatedPath)!);
            using var image = SKImage.FromBitmap(annotated);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(annotatedPath);
            data.SaveTo(stream);
            return CoreResult<bool>.Ok(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return CoreResult<bool>.Fail(new CoreError(
                CoreErrorCodes.ImageDiffFailed,
                $"Semantic annotation image could not be created: {exception.Message}",
                new Dictionary<string, string> { ["annotatedPath"] = annotatedPath }));
        }
    }

    private static bool[] CreateChangedMask(SKBitmap reference, SKBitmap current, double tolerance, out int maxDelta)
    {
        var changed = new bool[reference.Width * reference.Height];
        maxDelta = 0;
        for (var y = 0; y < reference.Height; y++)
        {
            for (var x = 0; x < reference.Width; x++)
            {
                var delta = MaxChannelDelta(reference.GetPixel(x, y), current.GetPixel(x, y));
                maxDelta = Math.Max(maxDelta, delta);
                changed[y * reference.Width + x] = delta > tolerance;
            }
        }

        return changed;
    }

    private static IReadOnlyList<ChangedComponent> ExtractComponents(
        bool[] changed,
        SKBitmap reference,
        SKBitmap current,
        int minChangedPixels)
    {
        var width = reference.Width;
        var height = reference.Height;
        var visited = new bool[changed.Length];
        var components = new List<ChangedComponent>();
        var queue = new Queue<int>();

        for (var index = 0; index < changed.Length; index++)
        {
            if (!changed[index] || visited[index])
            {
                continue;
            }

            visited[index] = true;
            queue.Enqueue(index);
            var minX = width;
            var minY = height;
            var maxX = 0;
            var maxY = 0;
            long changedPixels = 0;
            var maxDelta = 0;

            while (queue.Count > 0)
            {
                var currentIndex = queue.Dequeue();
                var x = currentIndex % width;
                var y = currentIndex / width;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                changedPixels++;
                maxDelta = Math.Max(maxDelta, MaxChannelDelta(reference.GetPixel(x, y), current.GetPixel(x, y)));

                Enqueue(x - 1, y);
                Enqueue(x + 1, y);
                Enqueue(x, y - 1);
                Enqueue(x, y + 1);
            }

            if (changedPixels >= minChangedPixels)
            {
                components.Add(new ChangedComponent(minX, minY, maxX - minX + 1, maxY - minY + 1, changedPixels, maxDelta));
            }
        }

        return components;

        void Enqueue(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            var nextIndex = y * width + x;
            if (!changed[nextIndex] || visited[nextIndex])
            {
                return;
            }

            visited[nextIndex] = true;
            queue.Enqueue(nextIndex);
        }
    }

    private static ImageBounds? FindContentBounds(SKBitmap bitmap, double tolerance)
    {
        var background = bitmap.GetPixel(0, 0);
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (MaxChannelDelta(background, bitmap.GetPixel(x, y)) <= tolerance)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX < minX || maxY < minY
            ? null
            : new ImageBounds(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static int CountContentBands(SKBitmap bitmap, double tolerance)
    {
        var background = bitmap.GetPixel(0, 0);
        var bands = 0;
        var inBand = false;
        for (var y = 0; y < bitmap.Height; y++)
        {
            var rowPixels = 0;
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (MaxChannelDelta(background, bitmap.GetPixel(x, y)) > tolerance)
                {
                    rowPixels++;
                }
            }

            var hasRowContent = rowPixels > Math.Max(2, bitmap.Width / 100);
            if (hasRowContent && !inBand)
            {
                bands++;
                inBand = true;
            }
            else if (!hasRowContent)
            {
                inBand = false;
            }
        }

        return bands;
    }

    private static IReadOnlyDictionary<string, double> CreateEdgeDeltas(ImageBounds reference, ImageBounds current)
    {
        return new Dictionary<string, double>
        {
            ["left"] = current.X - reference.X,
            ["top"] = current.Y - reference.Y,
            ["right"] = current.Right - reference.Right,
            ["bottom"] = current.Bottom - reference.Bottom
        };
    }

    private static IReadOnlyDictionary<string, double> CreatePaddingDeltas(
        ImageBounds reference,
        ImageBounds current,
        int width,
        int height)
    {
        return new Dictionary<string, double>
        {
            ["left"] = current.X - reference.X,
            ["top"] = current.Y - reference.Y,
            ["right"] = (width - current.Right) - (width - reference.Right),
            ["bottom"] = (height - current.Bottom) - (height - reference.Bottom)
        };
    }

    private static ScreenshotRegion Union(ImageBounds reference, ImageBounds current, int width, int height)
    {
        var x = Math.Max(0, Math.Min(reference.X, current.X));
        var y = Math.Max(0, Math.Min(reference.Y, current.Y));
        var right = Math.Min(width, Math.Max(reference.Right, current.Right));
        var bottom = Math.Min(height, Math.Max(reference.Bottom, current.Bottom));
        return new ScreenshotRegion(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y), "content-bounds");
    }

    private static ScreenshotRegion ToScreenshotRegion(ImageBounds bounds, string name)
    {
        return new ScreenshotRegion(bounds.X, bounds.Y, bounds.Width, bounds.Height, name);
    }

    private static ScreenshotRegion ToScreenshotRegion(ChangedComponent component, string name)
    {
        return new ScreenshotRegion(component.X, component.Y, component.Width, component.Height, name);
    }

    private static ScreenshotRegion Pad(ScreenshotRegion region, int width, int height)
    {
        var x = Math.Max(0, region.X - CropPadding);
        var y = Math.Max(0, region.Y - CropPadding);
        var right = Math.Min(width, region.X + region.Width + CropPadding);
        var bottom = Math.Min(height, region.Y + region.Height + CropPadding);
        return new ScreenshotRegion(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y), region.Name);
    }

    private static void WriteCrop(SKBitmap source, ScreenshotRegion region, string path, string label, bool annotate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var crop = new SKBitmap(region.Width, region.Height);
        using var canvas = new SKCanvas(crop);
        var sourceRect = new SKRect(region.X, region.Y, region.X + region.Width, region.Y + region.Height);
        canvas.DrawBitmap(source, sourceRect, new SKRect(0, 0, region.Width, region.Height));

        if (annotate)
        {
            using var paint = new SKPaint { Color = SKColors.Red, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
            using var labelPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            using var labelBackground = new SKPaint { Color = new SKColor(255, 255, 255, 225), Style = SKPaintStyle.Fill };
            using var font = new SKFont { Size = 13 };
            canvas.DrawRect(new SKRect(1, 1, region.Width - 1, region.Height - 1), paint);
            canvas.DrawRect(new SKRect(2, 2, Math.Min(region.Width - 2, 12 + label.Length * 7), 20), labelBackground);
            canvas.DrawText(label, 5, 16, SKTextAlign.Left, font, labelPaint);
        }

        using var image = SKImage.FromBitmap(crop);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private static void DrawLabeledRect(
        SKCanvas canvas,
        ScreenshotRegion region,
        string label,
        SKPaint rectanglePaint,
        SKPaint labelPaint,
        SKPaint labelBackground,
        SKFont font)
    {
        var rect = new SKRect(region.X, region.Y, region.X + region.Width, region.Y + region.Height);
        canvas.DrawRect(rect, rectanglePaint);
        var labelWidth = Math.Max(60, label.Length * 7);
        var labelRect = new SKRect(rect.Left, Math.Max(0, rect.Top - 20), Math.Min(rect.Left + labelWidth, canvas.LocalClipBounds.Right), Math.Max(18, rect.Top - 2));
        canvas.DrawRect(labelRect, labelBackground);
        canvas.DrawText(label, labelRect.Left + 3, labelRect.Bottom - 4, SKTextAlign.Left, font, labelPaint);
    }

    private static bool TouchesImageEdge(ChangedComponent component, int width, int height)
    {
        return component.X == 0
            || component.Y == 0
            || component.X + component.Width == width
            || component.Y + component.Height == height;
    }

    private static int MaxChannelDelta(SKColor first, SKColor second)
    {
        return Math.Max(
            Math.Max(Math.Abs(first.Red - second.Red), Math.Abs(first.Green - second.Green)),
            Math.Max(Math.Abs(first.Blue - second.Blue), Math.Abs(first.Alpha - second.Alpha)));
    }

    private static string ResolveOutputDirectory(SemanticScreenshotComparisonRequest request)
    {
        return request.OutputDirectory
            ?? Path.Combine(Path.GetTempPath(), "avascope-semantic-diff", SanitizeFileToken(request.RequestId));
    }

    private static string SanitizeFileToken(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(character => invalid.Contains(character) ? '-' : character).ToArray();
        var sanitized = new string(chars).Trim('-', ' ', '.');
        return string.IsNullOrWhiteSpace(sanitized) ? "item" : sanitized;
    }

    private static string FormatDelta(double value)
    {
        return value.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture);
    }

    private readonly record struct ChangedComponent(int X, int Y, int Width, int Height, long ChangedPixels, int MaxDelta);

    private sealed record ImageBounds(int X, int Y, int Width, int Height)
    {
        public int Right => X + Width;
        public int Bottom => Y + Height;
        public double CenterX => X + Width / 2d;
        public double CenterY => Y + Height / 2d;
    }
}
