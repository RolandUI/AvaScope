using AvaScope.Protocol;
using SkiaSharp;

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
        var response = result.Value!;
        var comparison = CompareScreenFrames(response.Rendered, response.Native, request.Policy, response.Consistency);
        var rendered = await Save(response.Rendered, "rendered.png"); var native = await Save(response.Native, "native.png");
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
            try
            {
                if (png.Length > 256 * 1024 || frame.PixelWidth < 1 || frame.PixelHeight < 1 || (long)frame.PixelWidth * frame.PixelHeight > 4194304)
                    throw new InvalidOperationException("Capture budget exceeded.");
                using (var codec = SKCodec.Create(new SKMemoryStream(png)))
                    if (codec is null || codec.Info.Width != frame.PixelWidth || codec.Info.Height != frame.PixelHeight) throw new InvalidOperationException("Capture dimensions disagree.");
                if (policy is not null)
                {
                    var masked = policy.MaskScreenshotPng(png, new(request.Target.SessionId, request.Target.TopLevelId, path, frame.PixelWidth, frame.PixelHeight, frame.CompletedAt));
                    png = masked.Png; frame = frame with { Masking = masked.Masking };
                }
                cancellationToken.ThrowIfCancellationRequested(); Directory.CreateDirectory(run);
                if (policy is not null)
                {
                    var verified = policy.PrepareRun(run, [path], Path.GetFileName(run));
                    if (!verified.Success) throw new InvalidOperationException("Evidence path authorization changed.");
                }
                await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { created = true; await file.WriteAsync(png, cancellationToken); }
                saved = true; return frame with { FilePath = path, Png = null };
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or OperationCanceledException)
            { return frame with { Status = "unavailable", Png = null, FilePath = null, Diagnostics = frame.Diagnostics.Append(new ProtocolError("screen_capture_save_failed", "The masked evidence could not be saved to the authorized local output directory.")).ToArray() }; }
            finally { if (created && !saved) File.Delete(path); }
        }
    }

    private static RuntimeScreenComparison CompareScreenFrames(RuntimeScreenFrame? rendered, RuntimeScreenFrame? native, RuntimeEvidencePolicy? policy, string consistency)
    {
        const string interpretation = "Descriptive comparison of sequential masked samples in the render-pixel grid; differences may reflect occlusion, native surfaces, resampling, timing or color management. No automatic defect or occlusion diagnosis.";
        if (rendered?.Png is null || native?.Png is null || rendered.Status != "captured" || native.Status != "captured"
            || !consistency.StartsWith("no_sampled_geometry_change", StringComparison.Ordinal)) return new("unavailable", 0, 0, 16, interpretation);
        if (rendered.Masking == "full_sensitive_mask" || native.Masking == "full_sensitive_mask") return new("privacy_masked", 0, 0, 16, interpretation);
        if (!Valid(rendered) || !Valid(native)) return new("unaligned", 0, 0, 16, interpretation);
        using var a = SKBitmap.Decode(rendered.Png); using var b = SKBitmap.Decode(native.Png);
        if (a is null || b is null || a.Width != b.Width || a.Height != b.Height || (long)a.Width * a.Height > 4194304) return new("unaligned", 0, 0, 16, interpretation);
        var masked = new bool[a.Width * a.Height];
        foreach (var mask in policy?.ScreenshotMaskRegions ?? [])
        {
            var left = Math.Clamp(mask.X, 0, a.Width); var right = (int)Math.Clamp((long)mask.X + mask.Width, left, a.Width);
            var top = Math.Clamp(mask.Y, 0, a.Height); var bottom = (int)Math.Clamp((long)mask.Y + mask.Height, top, a.Height);
            for (var row = top; row < bottom; row++) masked.AsSpan(row * a.Width + left, right - left).Fill(true);
        }
        long compared = 0, different = 0;
        for (var y = 0; y < a.Height; y++)
            for (var x = 0; x < a.Width; x++)
            {
                if (masked[y * a.Width + x]) continue;
                var first = a.GetPixel(x, y); var second = b.GetPixel(x, y);
                if (first.Alpha != 255 || second.Alpha != 255) continue;
                compared++;
                if (Math.Abs(first.Red - second.Red) > 16 || Math.Abs(first.Green - second.Green) > 16 || Math.Abs(first.Blue - second.Blue) > 16) different++;
            }
        return new(compared == 0 ? "no_comparable_pixels" : "compared", compared, different, 16, interpretation);

        static bool Valid(RuntimeScreenFrame frame)
        {
            if (frame.Png!.Length > 256 * 1024 || frame.PixelWidth < 1 || frame.PixelHeight < 1 || (long)frame.PixelWidth * frame.PixelHeight > 4194304) return false;
            using var codec = SKCodec.Create(new SKMemoryStream(frame.Png));
            return codec is not null && codec.Info.Width == frame.PixelWidth && codec.Info.Height == frame.PixelHeight;
        }
    }
}
