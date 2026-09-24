using AvaScope.Protocol;
using SkiaSharp;
using System.Diagnostics;
using System.Globalization;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    public async Task<CoreResult<RuntimeScreenCaptureResponse>> CaptureScreenAsync(RuntimeScreenCaptureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        var run = Path.Combine(request.OutputDirectory, "screen-" + Guid.NewGuid().ToString("N"));
        if (policy is not null)
        {
            var session = policy.AuthorizeSession(this, request.Target.SessionId);
            if (!session.Success) return CoreResult<RuntimeScreenCaptureResponse>.Fail(session.Error!);
            var authorization = policy.AuthorizeAction(SemanticWorkflowActions.Screenshot, null);
            if (!authorization.Success) return CoreResult<RuntimeScreenCaptureResponse>.Fail(authorization.Error!);
            var prepared = policy.PrepareRun(run, [], Path.GetFileName(run));
            if (!prepared.Success) return CoreResult<RuntimeScreenCaptureResponse>.Fail(prepared.Error!);
        }
        var manifest = FindSingleManifest(null, request.Target.SessionId);
        if (!manifest.Success) return CoreResult<RuntimeScreenCaptureResponse>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimeScreenCaptureResponse>(manifest.Value!, new(NewRequestId(), BridgeIpcMethods.CaptureScreen, screenCapture: request), cancellationToken,
            _operationTimeout + TimeSpan.FromMilliseconds(request.TimeoutMs));
        if (!result.Success) return result;
        using var processing = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        processing.CancelAfter(request.TimeoutMs);
        var processingElapsed = Stopwatch.StartNew();
        var response = result.Value!;
        var rendered = await Save(response.Rendered, "rendered.png"); var native = await Save(response.Native, "native.png");
        var comparison = CompareScreenFrames(response.Rendered, response.Native, request.Policy, response.Consistency, processing.Token);
        response = response with
        {
            Status = (rendered is null || rendered.Status == "captured") && (native is null || native.Status == "captured") ? "captured" : "partial",
            Rendered = rendered, Native = native, Comparison = comparison
        };
        return policy is null ? CoreResult<RuntimeScreenCaptureResponse>.Ok(response) : policy.Sanitize(response);

        async Task<RuntimeScreenFrame?> Save(RuntimeScreenFrame? frame, string name)
        {
            if (frame is null) return null;
            if (frame.Png is not { } png || frame.Status != "captured") return frame with { Png = null };
            var path = Path.Combine(run, name); var created = false; var saved = false;
            var stage = "validate_frame";
            try
            {
                processing.Token.ThrowIfCancellationRequested();
                if (png.Length > RuntimeScreenCaptureLimits.MaximumPngBytes || !RuntimeScreenCaptureLimits.AllowsDimensions(frame.PixelWidth, frame.PixelHeight))
                    throw new InvalidOperationException("Capture budget exceeded.");
                stage = "decode_header";
                using (var codec = SKCodec.Create(new SKMemoryStream(png)))
                    if (codec is null || codec.Info.Width != frame.PixelWidth || codec.Info.Height != frame.PixelHeight) throw new InvalidOperationException("Capture dimensions disagree.");
                if (policy is not null)
                {
                    stage = "mask";
                    var masked = policy.MaskScreenshotPng(png, new(request.Target.SessionId, request.Target.TopLevelId, path, frame.PixelWidth, frame.PixelHeight, frame.CompletedAt));
                    png = masked.Png; frame = frame with { Masking = masked.Masking };
                }
                if (png.Length > RuntimeScreenCaptureLimits.MaximumPngBytes) throw new InvalidOperationException("Masked capture budget exceeded.");
                stage = "prepare_directory";
                processing.Token.ThrowIfCancellationRequested(); Directory.CreateDirectory(run);
                if (policy is not null)
                {
                    stage = "authorize_path";
                    var verified = policy.PrepareRun(run, [path], Path.GetFileName(run));
                    if (!verified.Success) throw new InvalidOperationException("Evidence path authorization changed.");
                }
                stage = "write_file";
                await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { created = true; await file.WriteAsync(png, processing.Token); }
                saved = true; return frame with { FilePath = path, Png = null };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or OperationCanceledException)
            {
                return frame with
                {
                    Status = "unavailable", Png = null, FilePath = null,
                    Diagnostics = frame.Diagnostics.Append(new ProtocolError("screen_capture_save_failed",
                        "The masked evidence could not be saved to the authorized local output directory.",
                        new Dictionary<string, string>
                        {
                            ["stage"] = stage,
                            ["elapsedMs"] = processingElapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture),
                            ["timeoutMs"] = request.TimeoutMs.ToString(CultureInfo.InvariantCulture),
                            ["exceptionType"] = exception.GetType().Name,
                            ["hresult"] = exception.HResult.ToString("X8", CultureInfo.InvariantCulture),
                            ["cancellationSource"] = cancellationToken.IsCancellationRequested ? "caller"
                                : processing.IsCancellationRequested ? "processing_deadline" : "none"
                        })).ToArray()
                };
            }
            finally { if (created && !saved) File.Delete(path); }
        }
    }

    private static RuntimeScreenComparison CompareScreenFrames(RuntimeScreenFrame? rendered, RuntimeScreenFrame? native, RuntimeEvidencePolicy? policy, string consistency, CancellationToken cancellationToken)
    {
        const string interpretation = "Descriptive comparison of sequential masked samples in the render-pixel grid; differences may reflect occlusion, native surfaces, resampling, timing or color management. No automatic defect or occlusion diagnosis.";
        if (rendered?.Png is null || native?.Png is null || rendered.Status != "captured" || native.Status != "captured"
            || !consistency.StartsWith("no_sampled_geometry_change", StringComparison.Ordinal)) return new("unavailable", 0, 0, 16, interpretation);
        if (rendered.Masking == "full_sensitive_mask" || native.Masking == "full_sensitive_mask") return new("privacy_masked", 0, 0, 16, interpretation);
        if (!Valid(rendered) || !Valid(native)) return new("unaligned", 0, 0, 16, interpretation);
        if (cancellationToken.IsCancellationRequested) return new("timeout", 0, 0, 16, interpretation);
        using var a = SKBitmap.Decode(rendered.Png); using var b = SKBitmap.Decode(native.Png);
        if (a is null || b is null || a.Width != b.Width || a.Height != b.Height || !RuntimeScreenCaptureLimits.AllowsDimensions(a.Width, a.Height)) return new("unaligned", 0, 0, 16, interpretation);
        var width = a.Width; var height = a.Height;
        var firstPixels = a.Pixels; var secondPixels = b.Pixels;
        var masked = new bool[width * height];
        foreach (var mask in policy?.ScreenshotMaskRegions ?? [])
        {
            var left = Math.Clamp(mask.X, 0, width); var right = (int)Math.Clamp((long)mask.X + mask.Width, left, width);
            var top = Math.Clamp(mask.Y, 0, height); var bottom = (int)Math.Clamp((long)mask.Y + mask.Height, top, height);
            for (var row = top; row < bottom; row++) masked.AsSpan(row * width + left, right - left).Fill(true);
        }
        long compared = 0, different = 0;
        for (var y = 0; y < height; y++)
        {
            if (cancellationToken.IsCancellationRequested) return new("timeout", compared, different, 16, interpretation);
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                if (masked[index]) continue;
                var first = firstPixels[index]; var second = secondPixels[index];
                if (first.Alpha != 255 || second.Alpha != 255) continue;
                compared++;
                if (Math.Abs(first.Red - second.Red) > 16 || Math.Abs(first.Green - second.Green) > 16 || Math.Abs(first.Blue - second.Blue) > 16) different++;
            }
        }
        return new(compared == 0 ? "no_comparable_pixels" : "compared", compared, different, 16, interpretation);

        static bool Valid(RuntimeScreenFrame frame)
        {
            if (frame.Png!.Length > RuntimeScreenCaptureLimits.MaximumPngBytes || !RuntimeScreenCaptureLimits.AllowsDimensions(frame.PixelWidth, frame.PixelHeight)) return false;
            using var codec = SKCodec.Create(new SKMemoryStream(frame.Png));
            return codec is not null && codec.Info.Width == frame.PixelWidth && codec.Info.Height == frame.PixelHeight;
        }
    }
}
