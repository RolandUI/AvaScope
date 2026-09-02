using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Core;

public sealed class RuntimeEvidencePolicyEnforcer
{
    private const string RootMarkerName = ".avascope-evidence-root.json";
    private const string RunMarkerName = ".avascope-evidence-run.json";
    private const string Redacted = "[REDACTED]";
    private const string Excluded = "[EXCLUDED]";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private static readonly JsonSerializerOptions CompactJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> GestureActions = new(StringComparer.Ordinal)
    {
        SemanticWorkflowActions.Drag,
        SemanticWorkflowActions.Swipe,
        SemanticWorkflowActions.LongPress,
        SemanticWorkflowActions.PressAndHold
    };

    private readonly RuntimeEvidencePolicy _policy;
    private string? _runDirectory;

    public RuntimeEvidencePolicyEnforcer(RuntimeEvidencePolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public RuntimeEvidencePolicy Policy => _policy;

    public string? RunDirectory => _runDirectory;

    public string? ActionAuditPath => _policy.WriteActionAudit && _runDirectory is not null
        ? Path.Combine(_runDirectory, "action-audit.jsonl")
        : null;

    public CoreResult<IReadOnlyDictionary<string, string>> PrepareRun(
        string runDirectory,
        IEnumerable<string?> artifactPaths,
        string requestId)
    {
        try
        {
            var root = Path.GetFullPath(_policy.OwnedEvidenceRoot);
            var run = Path.GetFullPath(runDirectory);
            if (!string.Equals(SanitizeScalar(root), root, StringComparison.Ordinal)
                || !string.Equals(SanitizeScalar(run), run, StringComparison.Ordinal))
            {
                return Invalid("Evidence root paths cannot contain configured sensitive values.");
            }

            if (Path.GetPathRoot(root) is { } volumeRoot
                && PathsEqual(root, volumeRoot))
            {
                return Invalid("The owned evidence root cannot be a filesystem volume root.");
            }

            if (!IsStrictDescendant(root, run))
            {
                return Invalid("The workflow evidence directory must be a strict child of the configured owned evidence root.");
            }

            foreach (var path in artifactPaths.Where(static path => !string.IsNullOrWhiteSpace(path)))
            {
                var fullPath = Path.GetFullPath(path!);
                if (!string.Equals(SanitizeScalar(fullPath), fullPath, StringComparison.Ordinal))
                {
                    return Invalid("Evidence artifact paths cannot contain configured sensitive values.");
                }

                if (!IsSameOrDescendant(run, fullPath))
                {
                    return Invalid("Every evidence artifact path must remain inside the policy-owned workflow directory.");
                }
            }

            EnsureNoReparseTraversal(root);
            EnsureNoReparseTraversal(run);
            Directory.CreateDirectory(root);
            EnsureNoReparseTraversal(root);
            var ownershipId = ReadOrCreateOwnershipId(root);
            var requestFingerprint = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(requestId))).ToLowerInvariant();
            if (Directory.Exists(run))
            {
                EnsureNoReparseTraversal(run);
                if (HasReparsePoint(run))
                {
                    return Invalid("Policy-owned workflow directories cannot contain reparse points.");
                }

                var existingMarker = Path.Combine(run, RunMarkerName);
                if (File.Exists(existingMarker))
                {
                    if (!IsOwnedRun(run, ownershipId, requestFingerprint))
                    {
                        return Invalid("An existing evidence run must have matching ownership and request markers.");
                    }
                }
                else if (Directory.EnumerateFileSystemEntries(run).Any())
                {
                    return Invalid("A non-empty unowned directory cannot be claimed as an evidence run.");
                }
            }
            else
            {
                Directory.CreateDirectory(run);
                EnsureNoReparseTraversal(run);
            }

            EnsureMarkerIsNotReparsePoint(Path.Combine(run, RunMarkerName));
            if (!File.Exists(Path.Combine(run, RunMarkerName)))
            {
                File.WriteAllText(
                    Path.Combine(run, RunMarkerName),
                    JsonSerializer.Serialize(
                        new EvidenceRunMarker(
                            "avascope.runtime-evidence-run",
                            1,
                            ownershipId,
                            requestFingerprint),
                        CompactJsonOptions),
                    Encoding.UTF8);
            }
            _runDirectory = run;
            var deleted = ApplyRetention(root, run, ownershipId, DateTimeOffset.UtcNow);
            var actionAuditPath = ActionAuditPath;
            if (actionAuditPath is not null && !File.Exists(actionAuditPath))
            {
                using var audit = new FileStream(actionAuditPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            }

            var metadata = new Dictionary<string, string>
            {
                ["policy"] = "explicit_local_opt_in",
                ["storage"] = "local_filesystem",
                ["provenance"] = "avascope_runtime_evidence",
                ["networkUpload"] = "disabled",
                ["ownedEvidenceRoot"] = root,
                ["runDirectory"] = run,
                ["retentionDeletedRuns"] = deleted.ToString(CultureInfo.InvariantCulture),
                ["actionAudit"] = _policy.WriteActionAudit ? "local_redacted_jsonl" : "disabled"
            };
            if (actionAuditPath is not null)
            {
                metadata["actionAuditPath"] = actionAuditPath;
            }

            return CoreResult<IReadOnlyDictionary<string, string>>.Ok(metadata);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
        {
            return Invalid("The runtime evidence policy could not establish a safe local artifact boundary.");
        }
    }

    public CoreResult<IReadOnlyDictionary<string, string>> Authorize(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowPlan plan)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);

        if (_policy.AuthorizedSessionIds.Count > 0
            && !_policy.AuthorizedSessionIds.Contains(request.SessionId.Value, StringComparer.Ordinal))
        {
            return Unauthorized("The selected bridge session is outside the policy authorization set.");
        }

        var manifests = bridgeClient.ListSessionManifests()
            .Where(candidate => string.Equals(candidate.SessionId.Value, request.SessionId.Value, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (manifests.Length != 1)
        {
            return Unauthorized("The selected bridge session does not have one live local manifest.");
        }

        var manifest = manifests[0];

        if (!string.Equals(manifest.TransportScope, BridgeTransportScopes.LocalOnly, StringComparison.Ordinal))
        {
            return Unauthorized("Runtime evidence policy requires a local-only bridge transport.");
        }

        if (_policy.AuthorizedProcessIds.Count > 0 && !_policy.AuthorizedProcessIds.Contains(manifest.ProcessId))
        {
            return Unauthorized("The bridge process is outside the policy authorization set.");
        }

        foreach (var item in plan.Steps)
        {
            var authorization = AuthorizeAction(item.Action, customActionName: null);
            if (!authorization.Success)
            {
                return CoreResult<IReadOnlyDictionary<string, string>>.Fail(authorization.Error!);
            }
        }

        return CoreResult<IReadOnlyDictionary<string, string>>.Ok(new Dictionary<string, string>
        {
            ["authorizedSessionId"] = request.SessionId.Value,
            ["authorizedProcessId"] = manifest.ProcessId.ToString(CultureInfo.InvariantCulture),
            ["transportScope"] = manifest.TransportScope,
            ["actionAllowlist"] = string.Join(',', _policy.AllowedActions),
            ["gestures"] = _policy.AllowGestures ? "explicitly_allowed" : "denied",
            ["destructiveActions"] = _policy.AllowDestructiveActions ? "policy_allowed" : "denied"
        });
    }

    public CoreResult<bool> AuthorizeAction(string action, string? customActionName)
    {
        if (!_policy.AllowedActions.Contains(action, StringComparer.Ordinal))
        {
            return Disallowed("The workflow action is not present in the runtime evidence policy allowlist.");
        }

        if (GestureActions.Contains(action) && (!_policy.AllowGestures || !_policy.AllowDestructiveActions))
        {
            return Disallowed("Gesture execution requires explicit gesture and destructive-action policy authorization.");
        }

        if (string.Equals(action, SemanticWorkflowActions.CustomAction, StringComparison.Ordinal)
            && customActionName is not null
            && !_policy.AllowedCustomActions.Contains(customActionName, StringComparer.Ordinal))
        {
            return Disallowed("The application-defined action is not present in the runtime evidence policy allowlist.");
        }

        return CoreResult<bool>.Ok(true);
    }

    public bool AllowsDestructiveAction(bool requestAuthorization, bool isolatedState) =>
        _policy.AllowDestructiveActions && (requestAuthorization || isolatedState);

    public CoreResult<T> Sanitize<T>(T value)
        where T : class
    {
        try
        {
            var json = SanitizeJson(value);
            if (!json.Success)
            {
                return CoreResult<T>.Fail(json.Error!);
            }

            var sanitized = JsonSerializer.Deserialize<T>(json.Value!, JsonOptions);
            return sanitized is null
                ? RedactionFailed<T>()
                : CoreResult<T>.Ok(sanitized);
        }
        catch (Exception)
        {
            return RedactionFailed<T>();
        }
    }

    public CoreResult<string> SanitizeJson<T>(T value)
    {
        try
        {
            var node = JsonSerializer.SerializeToNode(value, JsonOptions);
            if (node is null)
            {
                return RedactionFailed<string>();
            }

            SanitizeNode(node, excludedControl: false);
            return CoreResult<string>.Ok(node.ToJsonString(JsonOptions));
        }
        catch (Exception)
        {
            return RedactionFailed<string>();
        }
    }

    public CoreResult<bool> AppendActionAudit(
        SemanticWorkflowRequest request,
        SemanticWorkflowStepResult result)
    {
        if (!_policy.WriteActionAudit)
        {
            return CoreResult<bool>.Ok(true);
        }

        if (ActionAuditPath is null)
        {
            return AuditFailed();
        }

        try
        {
            EnsureNoReparseTraversal(ActionAuditPath);
            EnsureMarkerIsNotReparsePoint(ActionAuditPath);
            var entry = new Dictionary<string, object?>
            {
                ["requestId"] = SanitizeScalar(request.RequestId),
                ["sessionId"] = SanitizeScalar(request.SessionId.Value),
                ["stepId"] = SanitizeScalar(result.StepId),
                ["action"] = result.Action,
                ["status"] = result.Status,
                ["message"] = SanitizeScalar(result.Message),
                ["executedAt"] = result.ExecutedAt,
                ["topLevelId"] = SanitizeScalar(result.Target?.TopLevelId ?? result.ResolvedTopLevelId),
                ["nodeId"] = SanitizeScalar(result.Target?.NodeId),
                ["customAction"] = SanitizeScalar(result.CustomAction?.ActionName),
                ["storage"] = "local_filesystem",
                ["networkUpload"] = "disabled"
            };
            var sanitized = SanitizeJson(entry);
            if (!sanitized.Success)
            {
                return AuditFailed();
            }

            var line = JsonNode.Parse(sanitized.Value!)!.ToJsonString(CompactJsonOptions);
            File.AppendAllText(ActionAuditPath, line + Environment.NewLine, Encoding.UTF8);
            return CoreResult<bool>.Ok(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return AuditFailed();
        }
    }

    public CoreResult<bool> SanitizeTextFile(string path)
    {
        var ownedArtifact = false;
        try
        {
            if (_runDirectory is null || !IsSameOrDescendant(_runDirectory, Path.GetFullPath(path)))
            {
                return RedactionFailed<bool>();
            }

            EnsureNoReparseTraversal(path);
            EnsureMarkerIsNotReparsePoint(path);
            ownedArtifact = true;

            if (!File.Exists(path))
            {
                return CoreResult<bool>.Ok(true);
            }

            var content = File.ReadAllText(path, Encoding.UTF8);
            File.WriteAllText(path, SanitizeScalar(content), Encoding.UTF8);
            return CoreResult<bool>.Ok(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (ownedArtifact)
            {
                DeleteUnmasked(path);
            }

            return RedactionFailed<bool>();
        }
    }

    public async Task<CoreResult<IReadOnlyDictionary<string, string>>> MaskScreenshotAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        ScreenshotResponse screenshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(screenshot);

        var ownedArtifact = false;
        try
        {
            if (_runDirectory is null || !IsSameOrDescendant(_runDirectory, Path.GetFullPath(screenshot.FilePath)))
            {
                return MaskFailed("Screenshot masking refused an artifact outside the policy-owned workflow directory.");
            }

            EnsureNoReparseTraversal(screenshot.FilePath);
            EnsureMarkerIsNotReparsePoint(screenshot.FilePath);
            ownedArtifact = true;

            var masks = _policy.ScreenshotMaskRegions.ToList();
            var maskSensitiveControls = _policy.ExcludedControlAutomationIds.Count > 0
                || _policy.RedactedAutomationIds.Count > 0
                || _policy.RedactedText.Count > 0;
            if (maskSensitiveControls)
            {
                var tree = await bridgeClient.VisualTreeAsync(
                    request.SessionId,
                    screenshot.TopLevelId,
                    SemanticWorkflowEvidenceOptions.MaximumTreeDepth,
                    cancellationToken);
                if (!tree.Success)
                {
                    DeleteUnmasked(screenshot.FilePath);
                    return MaskFailed("Screenshot masking could not resolve excluded controls; the unmasked artifact was removed.");
                }

                var rootBounds = tree.Value!.Root.Bounds;
                if (rootBounds is null || rootBounds.Width <= 0 || rootBounds.Height <= 0)
                {
                    DeleteUnmasked(screenshot.FilePath);
                    return MaskFailed("Screenshot masking could not map excluded controls; the unmasked artifact was removed.");
                }

                foreach (var node in EnumerateNodes(tree.Value.Root).Where(IsSensitiveNode))
                {
                    if (node.Bounds is null || node.Bounds.Width <= 0 || node.Bounds.Height <= 0)
                    {
                        DeleteUnmasked(screenshot.FilePath);
                        return MaskFailed("An excluded control had no maskable bounds; the unmasked artifact was removed.");
                    }

                    masks.Add(ToPixelRegion(node.Bounds, rootBounds, screenshot));
                }
            }

            if (masks.Count > 0)
            {
                MaskPng(screenshot.FilePath, masks);
            }

            return CoreResult<IReadOnlyDictionary<string, string>>.Ok(new Dictionary<string, string>
            {
                ["screenshotMasking"] = masks.Count == 0 ? "not_required" : "applied",
                ["maskedRegionCount"] = masks.Count.ToString(CultureInfo.InvariantCulture)
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (ownedArtifact)
            {
                DeleteUnmasked(screenshot.FilePath);
            }

            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            if (ownedArtifact)
            {
                DeleteUnmasked(screenshot.FilePath);
            }

            return MaskFailed("Screenshot masking failed closed; the unmasked artifact was removed.");
        }

        bool IsSensitiveNode(TreeNodeSummary node)
        {
            return node.AutomationId is not null
                    && (_policy.ExcludedControlAutomationIds.Contains(node.AutomationId, StringComparer.Ordinal)
                        || _policy.RedactedAutomationIds.Contains(node.AutomationId, StringComparer.Ordinal))
                || node.Text is not null
                    && _policy.RedactedText.Any(secret => node.Text.Contains(secret, StringComparison.Ordinal));
        }
    }

    public string SanitizeScalar(string? value, bool excludedControl = false)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (excludedControl)
        {
            return Excluded;
        }

        var sanitized = value;
        foreach (var secret in _policy.RedactedText
                     .Concat(_policy.RedactedAutomationIds)
                     .Concat(_policy.ExcludedControlAutomationIds))
        {
            sanitized = sanitized.Replace(secret, Redacted, StringComparison.Ordinal);
        }

        return sanitized;
    }

    private int ApplyRetention(string root, string currentRun, string ownershipId, DateTimeOffset now)
    {
        if (_policy.RetentionMaxAgeMinutes is null && _policy.RetentionMaxOwnedRuns is null)
        {
            return 0;
        }

        var candidates = Directory.EnumerateDirectories(root)
            .Select(Path.GetFullPath)
            .Where(path => !PathsEqual(path, currentRun))
            .Where(path => IsStrictDescendant(root, path))
            .Where(path => IsOwnedRun(path, ownershipId))
            .Where(path => !HasReparsePoint(path))
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(static directory => directory.LastWriteTimeUtc)
            .ToArray();
        var retained = 1;
        var deleted = 0;
        foreach (var candidate in candidates)
        {
            retained++;
            var ageExpired = _policy.RetentionMaxAgeMinutes is { } minutes
                && now - candidate.LastWriteTimeUtc >= TimeSpan.FromMinutes(minutes);
            var countExpired = _policy.RetentionMaxOwnedRuns is { } maximum && retained > maximum;
            if (!ageExpired && !countExpired)
            {
                continue;
            }

            Directory.Delete(candidate.FullName, recursive: true);
            deleted++;
        }

        return deleted;
    }

    private static string ReadOrCreateOwnershipId(string root)
    {
        var path = Path.Combine(root, RootMarkerName);
        EnsureMarkerIsNotReparsePoint(path);
        if (File.Exists(path))
        {
            var existing = JsonSerializer.Deserialize<EvidenceRootMarker>(File.ReadAllText(path, Encoding.UTF8), CompactJsonOptions);
            if (existing is null
                || !string.Equals(existing.Kind, "avascope.runtime-evidence-root", StringComparison.Ordinal)
                || existing.Version != 1
                || !Guid.TryParseExact(existing.OwnershipId, "N", out _))
            {
                throw new IOException("Evidence root ownership marker is invalid.");
            }

            return existing.OwnershipId;
        }

        var ownershipId = Guid.NewGuid().ToString("N");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new EvidenceRootMarker("avascope.runtime-evidence-root", 1, ownershipId),
                CompactJsonOptions),
            Encoding.UTF8);
        return ownershipId;
    }

    private static bool IsOwnedRun(string directory, string ownershipId, string? requestFingerprint = null)
    {
        try
        {
            var markerPath = Path.Combine(directory, RunMarkerName);
            if (!File.Exists(markerPath))
            {
                return false;
            }

            EnsureMarkerIsNotReparsePoint(markerPath);

            var marker = JsonSerializer.Deserialize<EvidenceRunMarker>(
                File.ReadAllText(markerPath, Encoding.UTF8),
                CompactJsonOptions);
            return marker is not null
                && string.Equals(marker.Kind, "avascope.runtime-evidence-run", StringComparison.Ordinal)
                && marker.Version == 1
                && string.Equals(marker.OwnershipId, ownershipId, StringComparison.Ordinal)
                && marker.RequestFingerprint.Length == 64
                && (requestFingerprint is null
                    || string.Equals(marker.RequestFingerprint, requestFingerprint, StringComparison.Ordinal));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return false;
        }
    }

    private void SanitizeNode(JsonNode node, bool excludedControl)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var excludesObject = excludedControl
                    || (obj.TryGetPropertyValue("automationId", out var automationIdNode)
                        && automationIdNode is JsonValue automationIdValue
                        && automationIdValue.TryGetValue<string>(out var automationId)
                        && automationId is not null
                        && _policy.ExcludedControlAutomationIds.Contains(automationId, StringComparer.Ordinal));
                foreach (var property in obj.ToArray())
                {
                    if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        obj[property.Key] = SanitizeScalar(text, excludesObject);
                    }
                    else if (property.Value is not null)
                    {
                        SanitizeNode(property.Value, excludesObject);
                    }

                    var sanitizedName = SanitizeScalar(property.Key);
                    if (!string.Equals(sanitizedName, property.Key, StringComparison.Ordinal))
                    {
                        var sanitizedValue = obj[property.Key];
                        obj.Remove(property.Key);
                        obj[sanitizedName] = sanitizedValue;
                    }
                }

                break;
            }
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        array[index] = SanitizeScalar(text, excludedControl);
                    }
                    else if (array[index] is { } item)
                    {
                        SanitizeNode(item, excludedControl);
                    }
                }

                break;
        }
    }

    private static ScreenshotRegion ToPixelRegion(NodeBounds bounds, NodeBounds root, ScreenshotResponse screenshot)
    {
        var scaleX = screenshot.PixelWidth / root.Width;
        var scaleY = screenshot.PixelHeight / root.Height;
        var x = (int)Math.Floor((bounds.X - root.X) * scaleX);
        var y = (int)Math.Floor((bounds.Y - root.Y) * scaleY);
        var right = (int)Math.Ceiling((bounds.X + bounds.Width - root.X) * scaleX);
        var bottom = (int)Math.Ceiling((bounds.Y + bounds.Height - root.Y) * scaleY);
        x = Math.Clamp(x, 0, screenshot.PixelWidth - 1);
        y = Math.Clamp(y, 0, screenshot.PixelHeight - 1);
        right = Math.Clamp(right, x + 1, screenshot.PixelWidth);
        bottom = Math.Clamp(bottom, y + 1, screenshot.PixelHeight);
        return new ScreenshotRegion(x, y, right - x, bottom - y, "excluded-control");
    }

    private static void MaskPng(string path, IReadOnlyList<ScreenshotRegion> masks)
    {
        using var bitmap = SKBitmap.Decode(path) ?? throw new InvalidOperationException("Screenshot image could not be decoded.");
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill, IsAntialias = false };
        foreach (var mask in masks)
        {
            var left = Math.Clamp(mask.X, 0, bitmap.Width);
            var top = Math.Clamp(mask.Y, 0, bitmap.Height);
            var right = Math.Clamp(mask.X + mask.Width, left, bitmap.Width);
            var bottom = Math.Clamp(mask.Y + mask.Height, top, bitmap.Height);
            if (right > left && bottom > top)
            {
                canvas.DrawRect(new SKRect(left, top, right, bottom), paint);
            }
        }

        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Masked screenshot could not be encoded.");
        var temporaryPath = path + ".masked.tmp";
        try
        {
            using (var stream = File.Create(temporaryPath))
            {
                data.SaveTo(stream);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static IEnumerable<TreeNodeSummary> EnumerateNodes(TreeNodeSummary node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in EnumerateNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsSameOrDescendant(string parent, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(parent), Path.GetFullPath(path));
        return !Path.IsPathRooted(relative)
            && !string.Equals(relative, "..", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static bool IsStrictDescendant(string parent, string path) =>
        IsSameOrDescendant(parent, path) && !PathsEqual(parent, path);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void EnsureNoReparseTraversal(string path)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current is not null)
        {
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IOException("Reparse points are not allowed in evidence paths.");
            }

            current = current.Parent;
        }
    }

    private static bool HasReparsePoint(string path)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(path));
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return true;
            }

            foreach (var item in directory.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
            {
                if (item.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return true;
                }

                if (item is DirectoryInfo child)
                {
                    pending.Push(child);
                }
            }
        }

        return false;
    }

    private static void EnsureMarkerIsNotReparsePoint(string path)
    {
        if (File.Exists(path)
            && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException("Evidence ownership markers cannot be reparse points.");
        }
    }

    private static void DeleteUnmasked(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static CoreResult<IReadOnlyDictionary<string, string>> Invalid(string message) =>
        CoreResult<IReadOnlyDictionary<string, string>>.Fail(new CoreError(CoreErrorCodes.RuntimeEvidencePolicyInvalid, message));

    private static CoreResult<IReadOnlyDictionary<string, string>> Unauthorized(string message) =>
        CoreResult<IReadOnlyDictionary<string, string>>.Fail(new CoreError(CoreErrorCodes.RuntimeEvidenceUnauthorized, message));

    private static CoreResult<bool> Disallowed(string message) =>
        CoreResult<bool>.Fail(new CoreError(CoreErrorCodes.RuntimeEvidenceActionDisallowed, message));

    private static CoreResult<T> RedactionFailed<T>() =>
        CoreResult<T>.Fail(new CoreError(
            CoreErrorCodes.RuntimeEvidenceRedactionFailed,
            "Runtime evidence redaction failed closed; the affected evidence was omitted."));

    private static CoreResult<IReadOnlyDictionary<string, string>> MaskFailed(string message) =>
        CoreResult<IReadOnlyDictionary<string, string>>.Fail(new CoreError(CoreErrorCodes.RuntimeEvidenceMaskFailed, message));

    private static CoreResult<bool> AuditFailed() =>
        CoreResult<bool>.Fail(new CoreError(
            CoreErrorCodes.RuntimeEvidenceAuditFailed,
            "The local redacted action audit could not be written."));

    private sealed record EvidenceRootMarker(string Kind, int Version, string OwnershipId);

    private sealed record EvidenceRunMarker(
        string Kind,
        int Version,
        string OwnershipId,
        string RequestFingerprint);
}
