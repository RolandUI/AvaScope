using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Cli;

internal static class Program
{
    private const string InvalidCliArguments = "invalid_cli_arguments";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            WriteFailure(InvalidCliArguments, GetUsage());
            return 2;
        }

        return args[0] switch
        {
            "preview" => await Preview(args[1..]),
            "preview-animation" => await PreviewAnimation(args[1..]),
            "attach" => await Attach(args[1..]),
            "list-top-levels" => await ListTopLevels(args[1..]),
            "screenshot" => await Screenshot(args[1..]),
            "visual-tree" => await Tree(args[1..], TreeKinds.Visual, GetVisualTreeUsage()),
            "logical-tree" => await Tree(args[1..], TreeKinds.Logical, GetLogicalTreeUsage()),
            "inspect-node" => await InspectNode(args[1..]),
            "find-nodes" => await FindNodes(args[1..]),
            "input" => await Input(args[1..]),
            "mutate-node" => await MutateNode(args[1..]),
            "mutate-node-evidence" => await MutateNodeEvidence(args[1..]),
            "mutation-review" => await MutationReview(args[1..]),
            "close-session" => await CloseSession(args[1..]),
            "diagnostics" => await Diagnostics(args[1..]),
            "launch-app" => await LaunchApp(args[1..]),
            "doctor" => await Doctor(args[1..]),
            "reload" => await Reload(args[1..]),
            "create-preview-session" => await CreatePreviewSession(args[1..]),
            "list-preview-sessions" => ListPreviewSessions(args[1..]),
            "reload-preview-session" => await ReloadPreviewSession(args[1..]),
            "close-preview-session" => ClosePreviewSession(args[1..]),
            "watch-preview-session" => await WatchPreviewSession(args[1..]),
            "preview-viewer" => PreviewViewer(args[1..]),
            "baseline-create" => await BaselineCreate(args[1..]),
            "baseline-check" => await BaselineCheck(args[1..]),
            "cleanup" => Cleanup(args[1..]),
            "cleanup-bridge-sessions" => await CleanupBridgeSessions(args[1..]),
            "diff" => Diff(args[1..]),
            "assert-region" => AssertRegion(args[1..]),
            "mcp" => await Mcp(),
            _ => UnknownCommand(args[0])
        };
    }

    private static async Task<int> Mcp()
    {
        var mcpAssemblyPath = Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll");
        if (!File.Exists(mcpAssemblyPath))
        {
            WriteFailure("mcp_server_unavailable", $"MCP server assembly '{mcpAssemblyPath}' was not found.");
            return 1;
        }

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add(mcpAssemblyPath);

        if (!process.Start())
        {
            WriteFailure("mcp_server_unavailable", $"Could not start MCP server '{mcpAssemblyPath}'.");
            return 1;
        }

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static async Task<int> Preview(string[] args)
    {
        if (args.Length == 0)
        {
            WriteFailure(InvalidCliArguments, GetPreviewUsage());
            return 2;
        }

        var projectPath = args[0];
        var options = ParseOptions(args[1..], GetPreviewUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetPreviewUsage(),
                "view",
                "out",
                "width",
                "height",
                "dpi",
                "theme",
                "culture",
                "design-data-type",
                "profile",
                "profile-file",
                "variant",
                "sizes",
                "contact-sheet"))
        {
            return 2;
        }

        if (!TryApplyPreviewProfile(projectPath, options.Values, GetPreviewUsage(), out var effectiveOptions, out var profileError))
        {
            WriteResult(ToolResult<PreviewResponse>.Fail(profileError!));
            return 2;
        }

        if (!effectiveOptions.TryGetValue("view", out _)
            || !effectiveOptions.TryGetValue("out", out _))
        {
            WriteFailure(InvalidCliArguments, GetPreviewUsage());
            return 2;
        }

        if (!TryCreatePreviewRequest(projectPath, effectiveOptions, out var request, out var error))
        {
            WriteResult(ToolResult<PreviewResponse>.Fail(error!));
            return 2;
        }

        var previewHostClient = new PreviewHostClient();
        if (effectiveOptions.TryGetValue("sizes", out var sizesText))
        {
            if (!TryParsePreviewViewports(sizesText, out var viewports))
            {
                WriteFailure(InvalidCliArguments, "sizes must be a comma-separated list like 1440x900,1280x720.");
                return 2;
            }

            var batchResult = await previewHostClient.RenderBatchAsync(
                request!,
                viewports!,
                effectiveOptions.TryGetValue("contact-sheet", out var contactSheetPath)
                    ? Path.GetFullPath(contactSheetPath)
                    : null);
            WriteResult(batchResult.Success
                ? ToolResult<PreviewBatchResponse>.Ok(batchResult.Value!)
                : ToolResult<PreviewBatchResponse>.Fail(new ProtocolError(
                    batchResult.Error!.Code,
                    batchResult.Error.Message,
                    batchResult.Error.Details)));

            return batchResult.Success && batchResult.Value!.Entries.Any(static entry => entry.Render.Success)
                ? 0
                : 1;
        }

        var result = await previewHostClient.RenderAsync(request!);
        WriteResult(result.Success
            ? ToolResult<PreviewResponse>.Ok(result.Value!)
            : ToolResult<PreviewResponse>.Fail(new ProtocolError(
                result.Error!.Code,
                result.Error.Message,
                result.Error.Details)));

        return result.Success ? 0 : 1;
    }

    private static bool TryCreatePreviewRequest(
        string projectPath,
        IReadOnlyDictionary<string, string> options,
        out PreviewRequest? request,
        out ProtocolError? error)
    {
        request = null;
        error = null;

        double? width = null;
        if (options.TryGetValue("width", out var widthText))
        {
            if (!TryParsePositiveDouble(widthText, out var parsedWidth))
            {
                error = new ProtocolError(
                    InvalidCliArguments,
                    "Width, height, and dpi must be positive numbers.");
                return false;
            }

            width = parsedWidth;
        }

        double? height = null;
        if (options.TryGetValue("height", out var heightText))
        {
            if (!TryParsePositiveDouble(heightText, out var parsedHeight))
            {
                error = new ProtocolError(
                    InvalidCliArguments,
                    "Width, height, and dpi must be positive numbers.");
                return false;
            }

            height = parsedHeight;
        }

        if (!TryParsePositiveDouble(options.GetValueOrDefault("dpi", "96"), out var dpi))
        {
            error = new ProtocolError(
                InvalidCliArguments,
                "Width, height, and dpi must be positive numbers.");
            return false;
        }

        try
        {
            request = new PreviewRequest(
                Path.GetFullPath(options["out"]),
                width,
                height,
                dpi,
                Path.GetFullPath(projectPath),
                options["view"],
                options.GetValueOrDefault("theme"),
                options.GetValueOrDefault("culture"),
                options.GetValueOrDefault("design-data-type"));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or PathTooLongException)
        {
            error = new ProtocolError(CoreErrorCodes.InvalidPreviewRequest, exception.Message);
            return false;
        }
    }

    private static async Task<int> PreviewAnimation(string[] args)
    {
        if (args.Length == 0)
        {
            WriteFailure<PreviewAnimationResponse>(InvalidCliArguments, GetPreviewAnimationUsage());
            return 2;
        }

        var projectPath = args[0];
        var options = ParseOptions(args[1..], GetPreviewAnimationUsage());
        if (!options.Success)
        {
            WriteFailure<PreviewAnimationResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetPreviewAnimationUsage(),
                "view",
                "out",
                "width",
                "height",
                "dpi",
                "theme",
                "culture",
                "design-data-type",
                "profile",
                "profile-file",
                "variant",
                "time-offsets",
                "frame-strip",
                "viewer"))
        {
            return 2;
        }

        if (!TryApplyPreviewProfile(projectPath, options.Values, GetPreviewAnimationUsage(), out var effectiveOptions, out var profileError))
        {
            WriteResult(ToolResult<PreviewAnimationResponse>.Fail(profileError!));
            return 2;
        }

        if (!effectiveOptions.TryGetValue("view", out _)
            || !effectiveOptions.TryGetValue("out", out _)
            || !effectiveOptions.TryGetValue("time-offsets", out _))
        {
            WriteFailure<PreviewAnimationResponse>(InvalidCliArguments, GetPreviewAnimationUsage());
            return 2;
        }

        if (!TryCreatePreviewAnimationRequest(projectPath, effectiveOptions, out var request, out var error))
        {
            WriteResult(ToolResult<PreviewAnimationResponse>.Fail(error!));
            return 2;
        }

        var result = await new PreviewHostClient().RenderAnimationAsync(request!);
        WriteResult(result);
        return result.Success && result.Value!.Frames.Any(static frame => frame.Render.Success) ? 0 : 1;
    }

    private static bool TryCreatePreviewAnimationRequest(
        string projectPath,
        IReadOnlyDictionary<string, string> options,
        out PreviewAnimationRequest? request,
        out ProtocolError? error)
    {
        request = null;
        error = null;

        if (!TryParseAnimationTimeOffsets(options["time-offsets"], out var timeOffsetsMs))
        {
            error = new ProtocolError(
                InvalidCliArguments,
                $"time-offsets must be a comma-separated list of 0..{PreviewAnimationRequest.MaximumTimeOffsetMs} millisecond offsets.");
            return false;
        }

        double? width = null;
        if (options.TryGetValue("width", out var widthText))
        {
            if (!TryParsePositiveDouble(widthText, out var parsedWidth))
            {
                error = new ProtocolError(
                    InvalidCliArguments,
                    "Width, height, and dpi must be positive numbers.");
                return false;
            }

            width = parsedWidth;
        }

        double? height = null;
        if (options.TryGetValue("height", out var heightText))
        {
            if (!TryParsePositiveDouble(heightText, out var parsedHeight))
            {
                error = new ProtocolError(
                    InvalidCliArguments,
                    "Width, height, and dpi must be positive numbers.");
                return false;
            }

            height = parsedHeight;
        }

        if (!TryParsePositiveDouble(options.GetValueOrDefault("dpi", "96"), out var dpi))
        {
            error = new ProtocolError(
                InvalidCliArguments,
                "Width, height, and dpi must be positive numbers.");
            return false;
        }

        try
        {
            request = new PreviewAnimationRequest(
                Path.GetFullPath(options["out"]),
                timeOffsetsMs,
                width,
                height,
                dpi,
                Path.GetFullPath(projectPath),
                options["view"],
                options.GetValueOrDefault("theme"),
                options.GetValueOrDefault("culture"),
                options.GetValueOrDefault("design-data-type"),
                options.TryGetValue("frame-strip", out var frameStripPath) ? Path.GetFullPath(frameStripPath) : null,
                options.TryGetValue("viewer", out var viewerPath) ? Path.GetFullPath(viewerPath) : null);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or PathTooLongException)
        {
            error = new ProtocolError(CoreErrorCodes.InvalidPreviewRequest, exception.Message);
            return false;
        }
    }

    private static bool TryApplyPreviewProfile(
        string projectPath,
        IReadOnlyDictionary<string, string> options,
        string usage,
        out IReadOnlyDictionary<string, string> effectiveOptions,
        out ProtocolError? error)
    {
        effectiveOptions = options;
        error = null;

        if (!options.TryGetValue("profile", out var profileName))
        {
            if (options.ContainsKey("variant"))
            {
                error = new ProtocolError(InvalidCliArguments, "--variant requires --profile.");
                return false;
            }

            return true;
        }

        if (string.IsNullOrWhiteSpace(profileName))
        {
            error = new ProtocolError(InvalidCliArguments, usage);
            return false;
        }

        string profileFilePath;
        try
        {
            var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? Environment.CurrentDirectory;
            profileFilePath = options.TryGetValue("profile-file", out var configuredProfileFile)
                ? Path.GetFullPath(configuredProfileFile)
                : Path.Combine(projectDirectory, "avascope.preview.json");
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = new ProtocolError(InvalidCliArguments, exception.Message);
            return false;
        }

        if (!File.Exists(profileFilePath))
        {
            error = new ProtocolError(
                InvalidCliArguments,
                $"Preview profile file was not found: {profileFilePath}");
            return false;
        }

        Dictionary<string, string> profileOptions;
        try
        {
            if (!TryReadPreviewProfile(
                    profileFilePath,
                    profileName,
                    options.GetValueOrDefault("variant"),
                    out profileOptions!,
                    out var profileReadError))
            {
                error = profileReadError;
                return false;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = new ProtocolError(InvalidCliArguments, $"Preview profile file could not be read: {exception.Message}");
            return false;
        }

        foreach (var option in options)
        {
            profileOptions[option.Key] = option.Value;
        }

        effectiveOptions = profileOptions;
        return true;
    }

    private static bool TryReadPreviewProfile(
        string profileFilePath,
        string profileName,
        string? variantName,
        out Dictionary<string, string> profileOptions,
        out ProtocolError? error)
    {
        profileOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = null;

        using var document = JsonDocument.Parse(File.ReadAllText(profileFilePath));
        if (!document.RootElement.TryGetProperty("profiles", out var profilesElement)
            || profilesElement.ValueKind is not JsonValueKind.Object)
        {
            error = new ProtocolError(InvalidCliArguments, "Preview profile file must contain a 'profiles' object.");
            return false;
        }

        JsonElement? selectedProfile = null;
        foreach (var profileProperty in profilesElement.EnumerateObject())
        {
            if (string.Equals(profileProperty.Name, profileName, StringComparison.OrdinalIgnoreCase))
            {
                selectedProfile = profileProperty.Value;
                break;
            }
        }

        if (selectedProfile is null)
        {
            error = new ProtocolError(
                InvalidCliArguments,
                $"Preview profile '{profileName}' was not found in {Path.GetFullPath(profileFilePath)}.");
            return false;
        }

        if (selectedProfile.Value.ValueKind is not JsonValueKind.Object)
        {
            error = new ProtocolError(InvalidCliArguments, $"Preview profile '{profileName}' must be a JSON object.");
            return false;
        }

        var profileDirectory = Path.GetDirectoryName(Path.GetFullPath(profileFilePath)) ?? Environment.CurrentDirectory;
        foreach (var property in selectedProfile.Value.EnumerateObject())
        {
            if (string.Equals(property.Name, "variants", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryMapPreviewProfileProperty(property, profileDirectory, out var optionName, out var optionValue, out error))
            {
                return false;
            }

            profileOptions[optionName!] = optionValue!;
        }

        if (!TryApplyPreviewProfileVariant(
                selectedProfile.Value,
                profileFilePath,
                profileDirectory,
                profileName,
                variantName,
                profileOptions,
                out error))
        {
            return false;
        }

        return true;
    }

    private static bool TryApplyPreviewProfileVariant(
        JsonElement selectedProfile,
        string profileFilePath,
        string profileDirectory,
        string profileName,
        string? variantName,
        Dictionary<string, string> profileOptions,
        out ProtocolError? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(variantName))
        {
            return true;
        }

        if (!selectedProfile.TryGetProperty("variants", out var variantsElement)
            || variantsElement.ValueKind is not JsonValueKind.Object)
        {
            error = new ProtocolError(
                InvalidCliArguments,
                $"Preview profile '{profileName}' does not declare variants in {Path.GetFullPath(profileFilePath)}.");
            return false;
        }

        JsonElement? selectedVariant = null;
        foreach (var variantProperty in variantsElement.EnumerateObject())
        {
            if (string.Equals(variantProperty.Name, variantName, StringComparison.OrdinalIgnoreCase))
            {
                selectedVariant = variantProperty.Value;
                break;
            }
        }

        if (selectedVariant is null)
        {
            error = new ProtocolError(
                InvalidCliArguments,
                $"Preview profile variant '{variantName}' was not found in profile '{profileName}' from {Path.GetFullPath(profileFilePath)}.");
            return false;
        }

        if (selectedVariant.Value.ValueKind is not JsonValueKind.Object)
        {
            error = new ProtocolError(InvalidCliArguments, $"Preview profile variant '{variantName}' must be a JSON object.");
            return false;
        }

        foreach (var property in selectedVariant.Value.EnumerateObject())
        {
            if (string.Equals(property.Name, "variants", StringComparison.Ordinal))
            {
                error = new ProtocolError(InvalidCliArguments, "Preview profile variants cannot declare nested variants.");
                return false;
            }

            if (!TryMapPreviewProfileProperty(property, profileDirectory, out var optionName, out var optionValue, out error))
            {
                return false;
            }

            profileOptions[optionName!] = optionValue!;
        }

        return true;
    }

    private static bool TryMapPreviewProfileProperty(
        JsonProperty property,
        string profileDirectory,
        out string? optionName,
        out string? optionValue,
        out ProtocolError? error)
    {
        optionName = property.Name switch
        {
            "view" => "view",
            "out" => "out",
            "width" => "width",
            "height" => "height",
            "dpi" => "dpi",
            "theme" => "theme",
            "culture" => "culture",
            "designDataType" or "design-data-type" => "design-data-type",
            "sizes" => "sizes",
            "contactSheet" or "contact-sheet" => "contact-sheet",
            "timeOffsetsMs" or "time-offsets" => "time-offsets",
            "frameStripPath" or "frame-strip" => "frame-strip",
            "viewerPath" or "viewer" => "viewer",
            "displayName" or "display-name" => "display-name",
            _ => null
        };
        optionValue = null;
        error = null;

        if (optionName is null)
        {
            error = new ProtocolError(InvalidCliArguments, $"Preview profile property '{property.Name}' is not supported.");
            return false;
        }

        if (!TryReadPreviewProfileValue(property, out optionValue))
        {
            error = new ProtocolError(InvalidCliArguments, $"Preview profile property '{property.Name}' must be a string, number, or string array.");
            return false;
        }

        if (optionName is "out" or "contact-sheet" or "frame-strip" or "viewer" && !Path.IsPathRooted(optionValue!))
        {
            optionValue = Path.GetFullPath(Path.Combine(profileDirectory, optionValue!));
        }

        return true;
    }

    private static bool TryReadPreviewProfileValue(JsonProperty property, out string? value)
    {
        if (property.Value.ValueKind is JsonValueKind.Array)
        {
            var values = new List<string>();
            foreach (var item in property.Value.EnumerateArray())
            {
                if (item.ValueKind is not JsonValueKind.String)
                {
                    value = null;
                    return false;
                }

                var itemValue = item.GetString();
                if (string.IsNullOrWhiteSpace(itemValue))
                {
                    value = null;
                    return false;
                }

                values.Add(itemValue);
            }

            value = string.Join(',', values);
            return values.Count > 0;
        }

        value = property.Value.ValueKind switch
        {
            JsonValueKind.String => property.Value.GetString(),
            JsonValueKind.Number => property.Value.GetRawText(),
            _ => null
        };

        return !string.IsNullOrWhiteSpace(value);
    }

    private static async Task<int> CreatePreviewSession(string[] args)
    {
        if (args.Length == 0)
        {
            WriteFailure<PreviewSessionSummary>(InvalidCliArguments, GetCreatePreviewSessionUsage());
            return 2;
        }

        var projectPath = args[0];
        var options = ParseOptions(args[1..], GetCreatePreviewSessionUsage());
        if (!options.Success)
        {
            WriteFailure<PreviewSessionSummary>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetCreatePreviewSessionUsage(),
                "view",
                "out",
                "width",
                "height",
                "dpi",
                "theme",
                "culture",
                "design-data-type",
                "profile",
                "profile-file",
                "variant",
                "display-name"))
        {
            return 2;
        }

        if (!TryApplyPreviewProfile(projectPath, options.Values, GetCreatePreviewSessionUsage(), out var effectiveOptions, out var profileError))
        {
            WriteResult(ToolResult<PreviewSessionSummary>.Fail(profileError!));
            return 2;
        }

        if (!effectiveOptions.TryGetValue("view", out _)
            || !effectiveOptions.TryGetValue("out", out _))
        {
            WriteFailure<PreviewSessionSummary>(InvalidCliArguments, GetCreatePreviewSessionUsage());
            return 2;
        }

        if (!TryCreatePreviewRequest(projectPath, effectiveOptions, out var request, out var error))
        {
            WriteResult(ToolResult<PreviewSessionSummary>.Fail(error!));
            return 2;
        }

        var result = await CreatePreviewSessionRegistry().CreateAsync(
            request!,
            effectiveOptions.GetValueOrDefault("display-name"));
        WriteResult(result);
        return result.Success ? 0 : 1;
    }

    private static int ListPreviewSessions(string[] args)
    {
        var options = ParseOptions(args, GetListPreviewSessionsUsage());
        if (!options.Success)
        {
            WriteFailure<ListPreviewSessionsResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetListPreviewSessionsUsage()))
        {
            return 2;
        }

        WriteResult(ToolResult<ListPreviewSessionsResponse>.Ok(new ListPreviewSessionsResponse(
            CreatePreviewSessionRegistry().List())));
        return 0;
    }

    private static async Task<int> ReloadPreviewSession(string[] args)
    {
        var options = ParseOptions(args, GetReloadPreviewSessionUsage());
        if (!options.Success)
        {
            WriteFailure<PreviewSessionSummary>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetReloadPreviewSessionUsage(), "session")
            || !TryReadRequiredSessionId(options.Values, GetReloadPreviewSessionUsage(), out var sessionId))
        {
            return 2;
        }

        var result = await CreatePreviewSessionRegistry().ReloadAsync(sessionId!);
        WriteResult(result);
        return result.Success ? 0 : 1;
    }

    private static int ClosePreviewSession(string[] args)
    {
        var options = ParseOptions(args, GetClosePreviewSessionUsage());
        if (!options.Success)
        {
            WriteFailure<PreviewSessionSummary>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetClosePreviewSessionUsage(), "session")
            || !TryReadRequiredSessionId(options.Values, GetClosePreviewSessionUsage(), out var sessionId))
        {
            return 2;
        }

        var result = CreatePreviewSessionRegistry().Close(sessionId!);
        WriteResult(result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> WatchPreviewSession(string[] args)
    {
        var options = ParseOptions(args, GetWatchPreviewSessionUsage());
        if (!options.Success)
        {
            WriteFailure<PreviewWatchResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetWatchPreviewSessionUsage(),
                "session",
                "timeout-ms",
                "settle-ms",
                "max-reloads",
                "watch")
            || !TryReadRequiredSessionId(options.Values, GetWatchPreviewSessionUsage(), out var sessionId)
            || !TryReadRequiredPositiveInt(options.Values, "timeout-ms", GetWatchPreviewSessionUsage(), out var timeoutMilliseconds)
            || !TryReadOptionalPositiveInt(options.Values, "settle-ms", out var settleMilliseconds)
            || !TryReadOptionalPositiveInt(options.Values, "max-reloads", out var maxReloads))
        {
            return 2;
        }

        if (!TryReadOptionalWatchPaths(options.Values, out var watchPaths))
        {
            return 2;
        }

        PreviewSessionWatchOptions watchOptions;
        try
        {
            watchOptions = new PreviewSessionWatchOptions(
                TimeSpan.FromMilliseconds(timeoutMilliseconds),
                TimeSpan.FromMilliseconds(settleMilliseconds ?? 250),
                maxReloads ?? 1,
                watchPaths);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            WriteFailure<PreviewWatchResponse>(InvalidCliArguments, exception.Message);
            return 2;
        }

        var result = await new PreviewSessionWatcher(CreatePreviewSessionRegistry()).WatchAsync(
            sessionId!,
            watchOptions);
        WriteResult(result);
        return result.Success && result.Value!.ReloadCount > 0 && !result.Value.TimedOut ? 0 : 1;
    }

    private static int PreviewViewer(string[] args)
    {
        var options = ParseOptions(args, GetPreviewViewerUsage());
        if (!options.Success)
        {
            WriteFailure<PreviewViewerResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetPreviewViewerUsage(), "session", "out"))
        {
            return 2;
        }

        if (!options.Values.TryGetValue("session", out var sessionText)
            || string.IsNullOrWhiteSpace(sessionText))
        {
            WriteFailure<PreviewViewerResponse>(InvalidCliArguments, GetPreviewViewerUsage());
            return 2;
        }

        SessionId sessionId;
        try
        {
            sessionId = new SessionId(sessionText);
        }
        catch (ArgumentException exception)
        {
            WriteFailure<PreviewViewerResponse>(CoreErrorCodes.InvalidBridgeRequest, exception.Message);
            return 2;
        }

        var session = CreatePreviewSessionRegistry().Get(sessionId);
        if (!session.Success)
        {
            WriteResult(ToolResult<PreviewViewerResponse>.Fail(new ProtocolError(
                session.Error!.Code,
                session.Error.Message,
                session.Error.Details)));
            return 1;
        }

        var result = new PreviewViewerExporter().Export(
            session.Value!,
            options.Values.GetValueOrDefault("out"));
        WriteResult(result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> BaselineCreate(string[] args)
    {
        if (args.Length == 0)
        {
            WriteFailure<PreviewBaselineCreateResponse>(InvalidCliArguments, GetBaselineCreateUsage());
            return 2;
        }

        if (string.Equals(args[0], "--suite", StringComparison.OrdinalIgnoreCase))
        {
            return await BaselineSuiteCreate(args);
        }

        var projectPath = args[0];
        var options = ParseOptions(args[1..], GetBaselineCreateUsage());
        if (!options.Success)
        {
            WriteFailure<PreviewBaselineCreateResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetBaselineCreateUsage(),
                "view",
                "manifest",
                "sizes",
                "out-dir",
                "dpi",
                "theme",
                "culture",
                "design-data-type")
            || !TryReadRequiredOption(options.Values, "view", GetBaselineCreateUsage(), out var viewPath)
            || !TryReadRequiredOption(options.Values, "manifest", GetBaselineCreateUsage(), out var manifestPath)
            || !TryReadRequiredOption(options.Values, "sizes", GetBaselineCreateUsage(), out var sizesText))
        {
            return 2;
        }

        if (!TryParsePreviewViewports(sizesText!, out var viewports))
        {
            WriteFailure<PreviewBaselineCreateResponse>(
                InvalidCliArguments,
                "sizes must be a comma-separated list like 1440x900,1280x720.");
            return 2;
        }

        if (!TryParsePositiveDouble(options.Values.GetValueOrDefault("dpi", "96"), out var dpi))
        {
            WriteFailure<PreviewBaselineCreateResponse>(
                InvalidCliArguments,
                "dpi must be a positive number.");
            return 2;
        }

        var fullManifestPath = Path.GetFullPath(manifestPath!);
        var outputDirectory = options.Values.TryGetValue("out-dir", out var configuredOutputDirectory)
            ? Path.GetFullPath(configuredOutputDirectory)
            : Path.Combine(Path.GetDirectoryName(fullManifestPath) ?? Environment.CurrentDirectory, "baseline-images");
        PreviewRequest request;
        try
        {
            request = new PreviewRequest(
                Path.Combine(outputDirectory, "baseline.png"),
                dpi: dpi,
                projectPath: Path.GetFullPath(projectPath),
                viewPath: viewPath,
                themeVariant: options.Values.GetValueOrDefault("theme"),
                culture: options.Values.GetValueOrDefault("culture"),
                designDataType: options.Values.GetValueOrDefault("design-data-type"));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or PathTooLongException)
        {
            WriteFailure<PreviewBaselineCreateResponse>(CoreErrorCodes.InvalidPreviewRequest, exception.Message);
            return 2;
        }

        var result = await new PreviewBaselineManager(new PreviewHostClient()).CreateAsync(
            request,
            viewports!,
            fullManifestPath,
            outputDirectory);
        WriteResult(result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> BaselineSuiteCreate(string[] args)
    {
        var options = ParseOptions(args, GetBaselineCreateUsage());
        if (!options.Success)
        {
            WriteFailure<PreviewBaselineCreateResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetBaselineCreateUsage(),
                "suite",
                "manifest",
                "out-dir")
            || !TryReadRequiredOption(options.Values, "suite", GetBaselineCreateUsage(), out var suitePath)
            || !TryReadRequiredOption(options.Values, "manifest", GetBaselineCreateUsage(), out var manifestPath))
        {
            return 2;
        }

        var fullManifestPath = Path.GetFullPath(manifestPath!);
        var outputDirectory = options.Values.TryGetValue("out-dir", out var configuredOutputDirectory)
            ? Path.GetFullPath(configuredOutputDirectory)
            : Path.Combine(Path.GetDirectoryName(fullManifestPath) ?? Environment.CurrentDirectory, "baseline-images");

        var result = await new PreviewBaselineManager(new PreviewHostClient()).CreateSuiteAsync(
            Path.GetFullPath(suitePath!),
            fullManifestPath,
            outputDirectory);
        WriteResult(result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> BaselineCheck(string[] args)
    {
        var options = ParseOptions(args, GetBaselineCheckUsage());
        if (!options.Success)
        {
            WriteFailure<PreviewBaselineCheckResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetBaselineCheckUsage(),
                "manifest",
                "out-dir",
                "diff-dir",
                "tolerance",
                "report",
                "report-pack")
            || !TryReadRequiredOption(options.Values, "manifest", GetBaselineCheckUsage(), out var manifestPath))
        {
            return 2;
        }

        if (!TryParseDoubleInRange(options.Values.GetValueOrDefault("tolerance", "0"), 0, 255, out var tolerance))
        {
            WriteFailure<PreviewBaselineCheckResponse>(InvalidCliArguments, "tolerance must be between 0 and 255.");
            return 2;
        }

        var fullManifestPath = Path.GetFullPath(manifestPath!);
        var manifestDirectory = Path.GetDirectoryName(fullManifestPath) ?? Environment.CurrentDirectory;
        var outputDirectory = options.Values.TryGetValue("out-dir", out var configuredOutputDirectory)
            ? Path.GetFullPath(configuredOutputDirectory)
            : Path.Combine(manifestDirectory, "current-images");
        var diffDirectory = options.Values.TryGetValue("diff-dir", out var configuredDiffDirectory)
            ? Path.GetFullPath(configuredDiffDirectory)
            : Path.Combine(manifestDirectory, "diff-images");
        var reportPath = options.Values.GetValueOrDefault("report");
        var reportPackDirectory = options.Values.GetValueOrDefault("report-pack");

        var result = await new PreviewBaselineManager(new PreviewHostClient()).CheckAsync(
            fullManifestPath,
            outputDirectory,
            diffDirectory,
            tolerance,
            reportPath,
            reportPackDirectory);
        WriteResult(result);
        return result.Success && result.Value!.Passed ? 0 : 1;
    }

    private static PreviewSessionRegistry CreatePreviewSessionRegistry()
    {
        return new PreviewSessionRegistry(
            new SessionRegistry(),
            new PreviewHostClient(),
            TimeProvider.System,
            PreviewSessionStore.CreateDefault());
    }

    private static LocalBridgeClient CreateBridgeClient(IReadOnlyDictionary<string, string> options)
    {
        return new LocalBridgeClient(options.GetValueOrDefault("manifest-dir"));
    }

    private static async Task<int> Attach(string[] args)
    {
        var options = ParseOptions(args, GetAttachUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetAttachUsage(), "process", "process-name", "session", "manifest", "manifest-dir", "latest")
            || !TryReadOptionalBoolean(options.Values, "latest", out var latest))
        {
            return 2;
        }

        int? processId = null;
        if (options.Values.TryGetValue("process", out var processText))
        {
            if (!int.TryParse(processText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedProcessId)
                || parsedProcessId < 1)
            {
                WriteFailure(InvalidCliArguments, "Process id must be a positive integer.");
                return 2;
            }

            processId = parsedProcessId;
        }

        SessionId? sessionId = null;
        if (options.Values.TryGetValue("session", out var sessionText))
        {
            try
            {
                sessionId = new SessionId(sessionText);
            }
            catch (ArgumentException exception)
            {
                WriteFailure(CoreErrorCodes.InvalidBridgeRequest, exception.Message);
                return 2;
            }
        }

        var bridgeClient = CreateBridgeClient(options.Values);
        var result = latest
            ? await bridgeClient.AttachLatestToAppAsync(
                processId,
                options.Values.GetValueOrDefault("process-name"))
            : await bridgeClient.AttachToAppAsync(
                processId,
                sessionId,
                options.Values.GetValueOrDefault("process-name"),
                options.Values.GetValueOrDefault("manifest"));
        WriteResult(result);

        return result.Success ? 0 : 1;
    }

    private static async Task<int> ListTopLevels(string[] args)
    {
        var options = ParseOptions(args, GetListTopLevelsUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetListTopLevelsUsage(), "session", "manifest-dir")
            || !TryReadRequiredSessionId(options.Values, GetListTopLevelsUsage(), out var sessionId))
        {
            return 2;
        }

        var result = await CreateBridgeClient(options.Values).ListTopLevelsAsync(sessionId!);
        WriteResult(result);

        return result.Success ? 0 : 1;
    }

    private static async Task<int> Tree(string[] args, string treeKind, string usage)
    {
        var options = ParseOptions(args, usage);
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, usage, "session", "top-level", "max-depth", "manifest-dir")
            || !TryReadRequiredSessionId(options.Values, usage, out var sessionId)
            || !TryReadRequiredOption(options.Values, "top-level", usage, out var topLevelId)
            || !TryReadOptionalNonNegativeInt(options.Values, "max-depth", out var maxDepth))
        {
            return 2;
        }

        var client = CreateBridgeClient(options.Values);
        var result = string.Equals(treeKind, TreeKinds.Visual, StringComparison.Ordinal)
            ? await client.VisualTreeAsync(sessionId!, topLevelId!, maxDepth)
            : await client.LogicalTreeAsync(sessionId!, topLevelId!, maxDepth);
        WriteResult(result);

        return result.Success ? 0 : 1;
    }

    private static async Task<int> InspectNode(string[] args)
    {
        var options = ParseOptions(args, GetInspectNodeUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetInspectNodeUsage(), "session", "top-level", "node", "tree-kind", "manifest-dir")
            || !TryReadRequiredSessionId(options.Values, GetInspectNodeUsage(), out var sessionId)
            || !TryReadRequiredOption(options.Values, "top-level", GetInspectNodeUsage(), out var topLevelId)
            || !TryReadRequiredOption(options.Values, "node", GetInspectNodeUsage(), out var nodeId)
            || !TryReadOptionalTreeKind(options.Values, out var treeKind))
        {
            return 2;
        }

        var result = await CreateBridgeClient(options.Values).InspectNodeAsync(sessionId!, topLevelId!, treeKind, nodeId!);
        WriteResult(result);

        return result.Success ? 0 : 1;
    }

    private static async Task<int> FindNodes(string[] args)
    {
        var options = ParseOptions(args, GetFindNodesUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetFindNodesUsage(),
                "session",
                "top-level",
                "tree-kind",
                "type",
                "name",
                "automation-id",
                "text",
                "max-depth",
                "max-results",
                "manifest-dir")
            || !TryReadRequiredSessionId(options.Values, GetFindNodesUsage(), out var sessionId)
            || !TryReadRequiredOption(options.Values, "top-level", GetFindNodesUsage(), out var topLevelId)
            || !TryReadOptionalTreeKind(options.Values, out var treeKind)
            || !TryReadOptionalNonNegativeInt(options.Values, "max-depth", out var maxDepth)
            || !TryReadOptionalPositiveInt(options.Values, "max-results", out var maxResults))
        {
            return 2;
        }

        var nodeType = options.Values.GetValueOrDefault("type");
        var name = options.Values.GetValueOrDefault("name");
        var automationId = options.Values.GetValueOrDefault("automation-id");
        var text = options.Values.GetValueOrDefault("text");
        if (string.IsNullOrWhiteSpace(nodeType)
            && string.IsNullOrWhiteSpace(name)
            && string.IsNullOrWhiteSpace(automationId)
            && string.IsNullOrWhiteSpace(text))
        {
            WriteFailure(InvalidCliArguments, "At least one find filter is required.");
            return 2;
        }

        var result = await CreateBridgeClient(options.Values).FindNodesAsync(
            sessionId!,
            topLevelId!,
            treeKind,
            nodeType,
            name,
            automationId,
            text,
            maxDepth,
            maxResults);
        WriteResult(result);

        return result.Success ? 0 : 1;
    }

    private static async Task<int> Input(string[] args)
    {
        var options = ParseOptions(args, GetInputUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetInputUsage(),
                "session",
                "top-level",
                "action",
                "x",
                "y",
                "text",
                "target-node",
                "key",
                "modifiers",
                "manifest-dir")
            || !TryReadRequiredSessionId(options.Values, GetInputUsage(), out var sessionId)
            || !TryReadRequiredOption(options.Values, "top-level", GetInputUsage(), out var topLevelId)
            || !TryReadRequiredOption(options.Values, "action", GetInputUsage(), out var action)
            || !TryNormalizeInputAction(action!, out action)
            || !TryReadOptionalDouble(options.Values, "x", out var x)
            || !TryReadOptionalDouble(options.Values, "y", out var y))
        {
            return 2;
        }

        var inputText = options.Values.GetValueOrDefault("text");
        var targetNodeId = options.Values.GetValueOrDefault("target-node");
        var inputKey = options.Values.GetValueOrDefault("key");
        var keyModifiers = options.Values.GetValueOrDefault("modifiers");
        if (!ValidateInputActionArguments(action!, x, y, inputText, targetNodeId, inputKey))
        {
            return 2;
        }

        var result = await CreateBridgeClient(options.Values).InputAsync(
            sessionId!,
            topLevelId!,
            action!,
            x,
            y,
            inputText,
            targetNodeId,
            inputKey,
            keyModifiers);
        WriteResult(result);

        return result.Success ? 0 : 1;
    }

    private static async Task<int> MutateNode(string[] args)
    {
        var options = ParseOptions(args, GetMutateNodeUsage());
        if (!options.Success)
        {
            WriteFailure<RuntimeMutationResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetMutateNodeUsage(),
                "session",
                "top-level",
                "node",
                "tree-kind",
                "operation",
                "property",
                "value",
                "value-type",
                "class",
                "resource-key",
                "mutation-id",
                "request-id",
                "manifest-dir")
            || !TryReadRequiredSessionId(options.Values, GetMutateNodeUsage(), out var sessionId)
            || !TryReadRequiredOption(options.Values, "top-level", GetMutateNodeUsage(), out var topLevelId)
            || !TryReadRequiredOption(options.Values, "node", GetMutateNodeUsage(), out var nodeId)
            || !TryReadRequiredOption(options.Values, "operation", GetMutateNodeUsage(), out var operationKind)
            || !TryReadOptionalTreeKind(options.Values, out var treeKind))
        {
            return 2;
        }

        RuntimeMutationRequest request;
        try
        {
            request = new RuntimeMutationRequest(
                options.Values.GetValueOrDefault("request-id") ?? Guid.NewGuid().ToString("n"),
                new RuntimeTargetContext(sessionId!, topLevelId!, treeKind, nodeId!),
                new RuntimeMutationOperation(
                    operationKind!,
                    options.Values.GetValueOrDefault("property"),
                    options.Values.GetValueOrDefault("value"),
                    options.Values.GetValueOrDefault("value-type"),
                    options.Values.GetValueOrDefault("class"),
                    options.Values.GetValueOrDefault("resource-key"),
                    options.Values.GetValueOrDefault("mutation-id")),
                [
                    RuntimeMutationCapabilityCatalog.RuntimeMutationContract,
                    RuntimeMutationCapabilityCatalog.StyleLayoutMutation
                ]);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            WriteResult(ToolResult<RuntimeMutationResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidBridgeRequest,
                exception.Message)));
            return 2;
        }

        var result = await CreateBridgeClient(options.Values).MutateNodeAsync(sessionId!, request);
        WriteResult(result);

        return result.Success && IsMutationCliSuccess(result.Value!) ? 0 : 1;
    }

    private static async Task<int> MutateNodeEvidence(string[] args)
    {
        var options = ParseOptions(args, GetMutateNodeEvidenceUsage());
        if (!options.Success)
        {
            WriteFailure<RuntimeMutationEvidenceResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        var includeDiff = true;
        if (!ValidateOptions(
                options.Values,
                GetMutateNodeEvidenceUsage(),
                "session",
                "top-level",
                "node",
                "tree-kind",
                "operation",
                "property",
                "value",
                "value-type",
                "class",
                "resource-key",
                "mutation-id",
                "request-id",
                "out-dir",
                "max-depth",
                "diff",
                "tolerance",
                "manifest-dir")
            || !TryReadRequiredSessionId(options.Values, GetMutateNodeEvidenceUsage(), out var sessionId)
            || !TryReadRequiredOption(options.Values, "top-level", GetMutateNodeEvidenceUsage(), out var topLevelId)
            || !TryReadRequiredOption(options.Values, "node", GetMutateNodeEvidenceUsage(), out var nodeId)
            || !TryReadRequiredOption(options.Values, "operation", GetMutateNodeEvidenceUsage(), out var operationKind)
            || !TryReadRequiredOption(options.Values, "out-dir", GetMutateNodeEvidenceUsage(), out var outputDirectory)
            || !TryReadOptionalTreeKind(options.Values, out var treeKind)
            || !TryReadOptionalNonNegativeInt(options.Values, "max-depth", out var maxDepth)
            || (options.Values.ContainsKey("diff")
                && !TryReadOptionalBoolean(options.Values, "diff", out includeDiff)))
        {
            return 2;
        }

        if (!TryParseDoubleInRange(options.Values.GetValueOrDefault("tolerance", "0"), 0, 255, out var tolerance))
        {
            WriteFailure<RuntimeMutationEvidenceResponse>(InvalidCliArguments, "tolerance must be between 0 and 255.");
            return 2;
        }

        RuntimeMutationRequest request;
        try
        {
            request = new RuntimeMutationRequest(
                options.Values.GetValueOrDefault("request-id") ?? Guid.NewGuid().ToString("n"),
                new RuntimeTargetContext(sessionId!, topLevelId!, treeKind, nodeId!),
                new RuntimeMutationOperation(
                    operationKind!,
                    options.Values.GetValueOrDefault("property"),
                    options.Values.GetValueOrDefault("value"),
                    options.Values.GetValueOrDefault("value-type"),
                    options.Values.GetValueOrDefault("class"),
                    options.Values.GetValueOrDefault("resource-key"),
                    options.Values.GetValueOrDefault("mutation-id")),
                [
                    RuntimeMutationCapabilityCatalog.RuntimeMutationContract,
                    RuntimeMutationCapabilityCatalog.StyleLayoutMutation
                ]);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            WriteResult(ToolResult<RuntimeMutationEvidenceResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidBridgeRequest,
                exception.Message)));
            return 2;
        }

        var result = await new RuntimeMutationEvidenceRunner().CaptureAsync(
            CreateBridgeClient(options.Values),
            sessionId!,
            request,
            outputDirectory!,
            maxDepth ?? 8,
            includeDiff,
            tolerance);
        WriteResult(result);

        return result.Success && IsMutationCliSuccess(result.Value!.Mutation) ? 0 : 1;
    }

    private static async Task<int> MutationReview(string[] args)
    {
        var options = ParseOptions(args, GetMutationReviewUsage());
        if (!options.Success)
        {
            WriteFailure<RuntimeMutationReviewResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetMutationReviewUsage(),
                "session",
                "max-results",
                "out",
                "manifest-dir",
                "source-project",
                "source-view",
                "source-app",
                "source-profile")
            || !TryReadRequiredSessionId(options.Values, GetMutationReviewUsage(), out var sessionId)
            || !TryReadOptionalPositiveInt(options.Values, "max-results", out var maxResults))
        {
            return 2;
        }

        if (maxResults > RuntimeMutationReviewResponse.MaximumEntries)
        {
            WriteFailure<RuntimeMutationReviewResponse>(
                InvalidCliArguments,
                $"max-results must be between 1 and {RuntimeMutationReviewResponse.MaximumEntries.ToString(CultureInfo.InvariantCulture)}.");
            return 2;
        }

        var result = await CreateBridgeClient(options.Values).MutationReviewAsync(sessionId!, maxResults);
        if (!result.Success)
        {
            WriteResult(result);
            return 1;
        }

        var response = result.Value!;
        response = RuntimeSourceSuggestionBuilder.WithSourceContext(
            response,
            CreateSourceSuggestionContext(options.Values, "cli"));
        if (options.Values.TryGetValue("out", out var outputPath))
        {
            var artifact = new RuntimeMutationReviewExporter().ExportReview(response, outputPath);
            if (!artifact.Success)
            {
                WriteResult(ToolResult<RuntimeMutationReviewResponse>.Fail(new ProtocolError(
                    artifact.Error!.Code,
                    artifact.Error.Message,
                    artifact.Error.Details)));
                return 1;
            }

            response = WithReviewArtifact(response, artifact.Value!);
        }

        WriteResult(ToolResult<RuntimeMutationReviewResponse>.Ok(response));
        return 0;
    }

    private static async Task<int> Reload(string[] args)
    {
        var options = ParseOptions(args, GetReloadUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetReloadUsage(), "session", "manifest-dir")
            || !TryReadRequiredSessionId(options.Values, GetReloadUsage(), out var sessionId))
        {
            return 2;
        }

        var result = await CreateBridgeClient(options.Values).ReloadRuntimeAsync(sessionId!);
        WriteResult(result);

        return result.Success ? 0 : 1;
    }

    private static async Task<int> Diagnostics(string[] args)
    {
        var options = ParseOptions(args, GetDiagnosticsUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetDiagnosticsUsage(), "process", "process-name", "session", "manifest", "manifest-dir", "max-sessions")
            || !TryReadOptionalProcessId(options.Values, out var processId)
            || !TryReadOptionalSessionId(options.Values, out var sessionId)
            || !TryReadOptionalDiagnosticsMaxSessions(options.Values, out var maxSessions))
        {
            return 2;
        }

        var previewSessionDiagnostics = PreviewSessionStore.CreateDefault().GetDiagnostics();
        var result = await CreateBridgeClient(options.Values).DiagnosticsAsync(
            processId,
            sessionId,
            maxSessions,
            new PreviewHostClient().GetDiagnostics(),
            previewSessionDiagnostics,
            processName: options.Values.GetValueOrDefault("process-name"),
            manifestPath: options.Values.GetValueOrDefault("manifest"));
        WriteResult(result);

        return result.Success ? 0 : 1;
    }

    private static async Task<int> Doctor(string[] args)
    {
        var options = ParseOptions(args, GetDoctorUsage());
        if (!options.Success)
        {
            WriteFailure<DoctorResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetDoctorUsage(), "manifest-dir", "preview-session-store"))
        {
            return 2;
        }

        var manifestDirectory = Path.GetFullPath(
            options.Values.GetValueOrDefault("manifest-dir", BridgeSessionManifest.GetDefaultDirectory()));
        var previewSessionStoreDirectory = Path.GetFullPath(
            options.Values.GetValueOrDefault("preview-session-store", PreviewSessionStore.GetDefaultDirectory()));
        var previewSessionStore = new PreviewSessionStore(previewSessionStoreDirectory);
        var previewSessionDiagnostics = previewSessionStore.GetDiagnostics();
        var previewHost = new PreviewHostClient().GetDiagnostics();
        var bridgeClient = new LocalBridgeClient(manifestDirectory);
        var diagnosticsResult = await bridgeClient.DiagnosticsAsync(
            maxSessions: 100,
            previewHost: previewHost,
            previewSessions: previewSessionDiagnostics);

        if (!diagnosticsResult.Success)
        {
            WriteResult(ToolResult<DoctorResponse>.Fail(new ProtocolError(
                diagnosticsResult.Error!.Code,
                diagnosticsResult.Error.Message,
                diagnosticsResult.Error.Details)));
            return 1;
        }

        var diagnostics = diagnosticsResult.Value!;
        var issues = new List<ProtocolError>(diagnostics.Issues);
        var checks = new List<DoctorCheck>();
        var cliAssemblyPath = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrWhiteSpace(cliAssemblyPath))
        {
            cliAssemblyPath = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        }

        AddFileCheck(
            checks,
            issues,
            "cli_assembly",
            cliAssemblyPath,
            "CLI assembly is available.",
            "CLI assembly was not found.",
            "cli_assembly_unavailable");

        AddFileCheck(
            checks,
            issues,
            "mcp_assembly",
            Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll"),
            "MCP server assembly is available beside the CLI.",
            "MCP server assembly was not found beside the CLI.",
            "mcp_server_unavailable");

        AddPreviewHostCheck(checks, issues, previewHost);
        AddDirectoryCheck(
            checks,
            "bridge_manifest_directory",
            manifestDirectory,
            "Bridge manifest directory is readable.",
            "Bridge manifest directory does not exist yet; no runtime bridge sessions are discoverable.");
        AddBridgeSessionCheck(checks, issues, diagnostics.BridgeSessions);
        AddDirectoryCheck(
            checks,
            "preview_session_store",
            previewSessionStoreDirectory,
            "Preview session store directory is readable.",
            "Preview session store directory does not exist yet; no preview sessions are persisted.");
        AddPreviewSessionCheck(checks, issues, previewSessionDiagnostics);

        var status = issues.Count == 0
            ? DiagnosticStatuses.Available
            : DiagnosticStatuses.Unavailable;
        var response = new DoctorResponse(
            HealthResponse.Current(),
            DateTimeOffset.UtcNow,
            status,
            cliAssemblyPath,
            AppContext.BaseDirectory,
            manifestDirectory,
            previewSessionStoreDirectory,
            checks,
            issues,
            previewHost,
            diagnostics.BridgeSessions,
            previewSessionDiagnostics);

        WriteResult(ToolResult<DoctorResponse>.Ok(response));
        return issues.Count == 0 ? 0 : 1;
    }

    private static async Task<int> LaunchApp(string[] args)
    {
        var options = ParseOptions(args, GetLaunchAppUsage());
        if (!options.Success)
        {
            WriteFailure<LaunchAppResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetLaunchAppUsage(),
                "command",
                "args",
                "working-dir",
                "display-name",
                "manifest-dir",
                "out-dir",
                "env",
                "timeout-ms")
            || !TryReadRequiredOption(options.Values, "command", GetLaunchAppUsage(), out var command)
            || !TryReadOptionalPositiveInt(options.Values, "timeout-ms", out var timeoutMs)
            || !TryReadOptionalEnvironmentVariables(options.Values, out var environment))
        {
            return 2;
        }

        var result = await new BridgeAppLauncher().LaunchAsync(
            command!,
            options.Values.GetValueOrDefault("args"),
            options.Values.GetValueOrDefault("working-dir"),
            options.Values.GetValueOrDefault("display-name"),
            options.Values.GetValueOrDefault("manifest-dir"),
            options.Values.GetValueOrDefault("out-dir"),
            environment,
            timeoutMs is null ? null : TimeSpan.FromMilliseconds(timeoutMs.Value));
        WriteResult(result);
        return result.Success ? 0 : 1;
    }

    private static int Cleanup(string[] args)
    {
        var options = ParseOptions(args, GetCleanupUsage());
        if (!options.Success)
        {
            WriteFailure<PreviewCleanupResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetCleanupUsage()))
        {
            return 2;
        }

        var result = PreviewSessionStore.CreateDefault().CleanupStale();
        WriteResult(result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> CleanupBridgeSessions(string[] args)
    {
        var options = ParseOptions(args, GetCleanupBridgeSessionsUsage());
        if (!options.Success)
        {
            WriteFailure<BridgeCleanupResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetCleanupBridgeSessionsUsage(), "manifest-dir"))
        {
            return 2;
        }

        var result = await CreateBridgeClient(options.Values).CleanupBridgeManifestsAsync();
        WriteResult(result);
        return result.Success && result.Value!.Issues.Count == 0 ? 0 : 1;
    }

    private static int Diff(string[] args)
    {
        var options = ParseOptions(args, GetDiffUsage());
        if (!options.Success)
        {
            WriteFailure<PreviewDiffResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetDiffUsage(), "baseline", "current", "out", "tolerance")
            || !TryReadRequiredOption(options.Values, "baseline", GetDiffUsage(), out var baselinePath)
            || !TryReadRequiredOption(options.Values, "current", GetDiffUsage(), out var currentPath)
            || !TryReadRequiredOption(options.Values, "out", GetDiffUsage(), out var diffPath))
        {
            return 2;
        }

        if (!TryParseDoubleInRange(options.Values.GetValueOrDefault("tolerance", "0"), 0, 255, out var tolerance))
        {
            WriteFailure<PreviewDiffResponse>(InvalidCliArguments, "tolerance must be between 0 and 255.");
            return 2;
        }

        var result = new PreviewImageDiffer().Compare(
            baselinePath!,
            currentPath!,
            diffPath,
            tolerance);
        WriteResult(result);
        return result.Success && result.Value!.Passed ? 0 : 1;
    }

    private static int AssertRegion(string[] args)
    {
        var options = ParseOptions(args, GetAssertRegionUsage());
        if (!options.Success)
        {
            WriteFailure<ScreenshotRegionAssertionResponse>(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(
                options.Values,
                GetAssertRegionUsage(),
                "image",
                "assert",
                "x",
                "y",
                "width",
                "height",
                "baseline",
                "crop-out",
                "tolerance",
                "min-changed-pixels",
                "mostly-blank-max-nonblank-percent")
            || !TryReadRequiredOption(options.Values, "image", GetAssertRegionUsage(), out var imagePath)
            || !TryReadRequiredOption(options.Values, "assert", GetAssertRegionUsage(), out var assertion)
            || !TryReadRequiredNonNegativeInt(options.Values, "x", GetAssertRegionUsage(), out var x)
            || !TryReadRequiredNonNegativeInt(options.Values, "y", GetAssertRegionUsage(), out var y)
            || !TryReadRequiredPositiveInt(options.Values, "width", GetAssertRegionUsage(), out var width)
            || !TryReadRequiredPositiveInt(options.Values, "height", GetAssertRegionUsage(), out var height))
        {
            return 2;
        }

        if (!TryParseDoubleInRange(options.Values.GetValueOrDefault("tolerance", "0"), 0, 255, out var tolerance))
        {
            WriteFailure<ScreenshotRegionAssertionResponse>(InvalidCliArguments, "tolerance must be between 0 and 255.");
            return 2;
        }

        if (!TryParseDoubleInRange(options.Values.GetValueOrDefault("mostly-blank-max-nonblank-percent", "1"), 0, 100, out var mostlyBlankThreshold))
        {
            WriteFailure<ScreenshotRegionAssertionResponse>(
                InvalidCliArguments,
                "mostly-blank-max-nonblank-percent must be between 0 and 100.");
            return 2;
        }

        long? minChangedPixels = null;
        if (options.Values.TryGetValue("min-changed-pixels", out var minChangedPixelsText))
        {
            if (!long.TryParse(minChangedPixelsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                || parsed < 1)
            {
                WriteFailure<ScreenshotRegionAssertionResponse>(InvalidCliArguments, "min-changed-pixels must be a positive integer.");
                return 2;
            }

            minChangedPixels = parsed;
        }

        var result = new ScreenshotRegionAsserter().Assert(
            imagePath!,
            new ScreenshotRegion(x, y, width, height),
            assertion!,
            options.Values.GetValueOrDefault("baseline"),
            options.Values.GetValueOrDefault("crop-out"),
            tolerance,
            minChangedPixels,
            mostlyBlankThreshold);
        WriteResult(result);
        return result.Success && result.Value!.Passed ? 0 : 1;
    }

    private static async Task<int> CloseSession(string[] args)
    {
        var options = ParseOptions(args, GetCloseSessionUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetCloseSessionUsage(), "session", "manifest-dir")
            || !TryReadRequiredSessionId(options.Values, GetCloseSessionUsage(), out var sessionId))
        {
            return 2;
        }

        var result = await CreateBridgeClient(options.Values).CloseSessionAsync(sessionId!);
        WriteResult(result);

        return result.Success ? 0 : 1;
    }

    private static async Task<int> Screenshot(string[] args)
    {
        var options = ParseOptions(args, GetScreenshotUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetScreenshotUsage(), "session", "top-level", "out", "manifest-dir")
            || !TryReadRequiredSessionId(options.Values, GetScreenshotUsage(), out var sessionId)
            || !TryReadRequiredOption(options.Values, "top-level", GetScreenshotUsage(), out var topLevelId)
            || !TryReadRequiredOption(options.Values, "out", GetScreenshotUsage(), out var outputPath))
        {
            return 2;
        }

        var result = await CreateBridgeClient(options.Values).CaptureScreenshotAsync(sessionId!, topLevelId!, outputPath!);
        WriteResult(result);

        return result.Success ? 0 : 1;
    }

    private static OptionParseResult ParseOptions(IReadOnlyList<string> args, string usage)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Count; index += 2)
        {
            var key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Count)
            {
                return OptionParseResult.Fail(usage);
            }

            values[key[2..]] = args[index + 1];
        }

        return OptionParseResult.Ok(values);
    }

    private static bool TryParsePositiveDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && value > 0;
    }

    private static bool TryParseDoubleInRange(string text, double minimum, double maximum, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value)
            && value >= minimum
            && value <= maximum;
    }

    private static bool TryParsePreviewViewports(string text, out IReadOnlyList<PreviewViewport>? viewports)
    {
        viewports = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parsed = new List<PreviewViewport>();
        foreach (var token in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(['x', 'X'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !TryParsePositiveDouble(parts[0], out var width)
                || !TryParsePositiveDouble(parts[1], out var height))
            {
                return false;
            }

            parsed.Add(new PreviewViewport(width, height));
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        viewports = parsed;
        return true;
    }

    private static bool TryParseAnimationTimeOffsets(string text, out IReadOnlyList<int>? timeOffsetsMs)
    {
        timeOffsetsMs = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parsed = new List<int>();
        foreach (var token in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset)
                || offset < 0
                || offset > PreviewAnimationRequest.MaximumTimeOffsetMs)
            {
                return false;
            }

            parsed.Add(offset);
        }

        if (parsed.Count == 0 || parsed.Count > PreviewAnimationRequest.MaximumFrameCount)
        {
            return false;
        }

        timeOffsetsMs = parsed;
        return true;
    }

    private static bool ValidateOptions(
        IReadOnlyDictionary<string, string> options,
        string usage,
        params string[] allowedOptions)
    {
        foreach (var key in options.Keys)
        {
            if (!allowedOptions.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                WriteFailure(InvalidCliArguments, usage);
                return false;
            }
        }

        return true;
    }

    private static void AddFileCheck(
        ICollection<DoctorCheck> checks,
        ICollection<ProtocolError> issues,
        string name,
        string path,
        string availableMessage,
        string unavailableMessage,
        string unavailableCode)
    {
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            checks.Add(new DoctorCheck(name, DiagnosticStatuses.Available, availableMessage, fullPath));
            return;
        }

        var error = new ProtocolError(unavailableCode, unavailableMessage);
        issues.Add(error);
        checks.Add(new DoctorCheck(name, DiagnosticStatuses.Unavailable, unavailableMessage, fullPath, error));
    }

    private static void AddPreviewHostCheck(
        ICollection<DoctorCheck> checks,
        ICollection<ProtocolError> issues,
        PreviewHostDiagnostic previewHost)
    {
        if (string.Equals(previewHost.Status, DiagnosticStatuses.Available, StringComparison.Ordinal))
        {
            checks.Add(new DoctorCheck(
                "preview_host",
                DiagnosticStatuses.Available,
                "Preview host assembly is available and configured for isolated child-process rendering.",
                previewHost.HostAssemblyPath));
            return;
        }

        var error = previewHost.Error ?? new ProtocolError(
            CoreErrorCodes.PreviewHostUnavailable,
            "Preview host is not available.");
        issues.Add(error);
        checks.Add(new DoctorCheck(
            "preview_host",
            previewHost.Status,
            error.Message,
            previewHost.HostAssemblyPath,
            error));
    }

    private static void AddDirectoryCheck(
        ICollection<DoctorCheck> checks,
        string name,
        string path,
        string availableMessage,
        string missingMessage)
    {
        var fullPath = Path.GetFullPath(path);
        checks.Add(Directory.Exists(fullPath)
            ? new DoctorCheck(name, DiagnosticStatuses.Available, availableMessage, fullPath)
            : new DoctorCheck(name, DiagnosticStatuses.Available, missingMessage, fullPath));
    }

    private static void AddBridgeSessionCheck(
        ICollection<DoctorCheck> checks,
        ICollection<ProtocolError> issues,
        IReadOnlyList<BridgeSessionDiagnostic> bridgeSessions)
    {
        var problematic = bridgeSessions
            .Where(static session => session.Status is not DiagnosticStatuses.Available)
            .ToArray();
        foreach (var session in problematic)
        {
            if (session.Error is not null)
            {
                issues.Add(session.Error);
            }
        }

        if (problematic.Length == 0)
        {
            checks.Add(new DoctorCheck(
                "bridge_sessions",
                DiagnosticStatuses.Available,
                bridgeSessions.Count == 0
                    ? "No runtime bridge sessions are currently discoverable."
                    : $"{bridgeSessions.Count} runtime bridge session(s) are available."));
            return;
        }

        checks.Add(new DoctorCheck(
            "bridge_sessions",
            DiagnosticStatuses.Unavailable,
            $"{problematic.Length} runtime bridge session diagnostic record(s) need attention."));
    }

    private static void AddPreviewSessionCheck(
        ICollection<DoctorCheck> checks,
        ICollection<ProtocolError> issues,
        IReadOnlyList<PreviewSessionDiagnostic> previewSessions)
    {
        var problematic = previewSessions
            .Where(static session => session.Status is not DiagnosticStatuses.Available)
            .ToArray();
        foreach (var session in problematic)
        {
            if (session.Error is not null)
            {
                issues.Add(session.Error);
            }
        }

        if (problematic.Length == 0)
        {
            checks.Add(new DoctorCheck(
                "preview_sessions",
                DiagnosticStatuses.Available,
                previewSessions.Count == 0
                    ? "No preview-session records are currently persisted."
                    : $"{previewSessions.Count} preview-session record(s) are available."));
            return;
        }

        checks.Add(new DoctorCheck(
            "preview_sessions",
            DiagnosticStatuses.Unavailable,
            $"{problematic.Length} preview-session diagnostic record(s) need attention."));
    }

    private static bool TryReadOptionalProcessId(
        IReadOnlyDictionary<string, string> options,
        out int? processId)
    {
        processId = null;
        if (!options.TryGetValue("process", out var processText))
        {
            return true;
        }

        if (!int.TryParse(processText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedProcessId)
            || parsedProcessId < 1)
        {
            WriteFailure(InvalidCliArguments, "Process id must be a positive integer.");
            return false;
        }

        processId = parsedProcessId;
        return true;
    }

    private static bool TryReadOptionalSessionId(
        IReadOnlyDictionary<string, string> options,
        out SessionId? sessionId)
    {
        sessionId = null;
        if (!options.TryGetValue("session", out var sessionText))
        {
            return true;
        }

        try
        {
            sessionId = new SessionId(sessionText);
            return true;
        }
        catch (ArgumentException exception)
        {
            WriteFailure(CoreErrorCodes.InvalidBridgeRequest, exception.Message);
            return false;
        }
    }

    private static bool TryReadOptionalDiagnosticsMaxSessions(
        IReadOnlyDictionary<string, string> options,
        out int maxSessions)
    {
        const int defaultMaxSessions = 50;
        const int maximumMaxSessions = 100;

        maxSessions = defaultMaxSessions;
        if (!options.TryGetValue("max-sessions", out var maxSessionsText))
        {
            return true;
        }

        if (!int.TryParse(maxSessionsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxSessions)
            || parsedMaxSessions is < 1 or > maximumMaxSessions)
        {
            WriteFailure(InvalidCliArguments, $"max-sessions must be between 1 and {maximumMaxSessions}.");
            return false;
        }

        maxSessions = parsedMaxSessions;
        return true;
    }

    private static bool TryReadOptionalBoolean(
        IReadOnlyDictionary<string, string> options,
        string optionName,
        out bool value)
    {
        value = false;
        if (!options.TryGetValue(optionName, out var text))
        {
            return true;
        }

        if (bool.TryParse(text, out value))
        {
            return true;
        }

        WriteFailure(InvalidCliArguments, $"{optionName} must be true or false.");
        return false;
    }

    private static bool TryReadOptionalNonNegativeInt(
        IReadOnlyDictionary<string, string> options,
        string optionName,
        out int? value)
    {
        value = null;
        if (!options.TryGetValue(optionName, out var text))
        {
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || parsed < 0)
        {
            WriteFailure(InvalidCliArguments, $"{optionName} must be a non-negative integer.");
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryReadOptionalDouble(
        IReadOnlyDictionary<string, string> options,
        string optionName,
        out double? value)
    {
        value = null;
        if (!options.TryGetValue(optionName, out var text))
        {
            return true;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed))
        {
            WriteFailure(InvalidCliArguments, $"{optionName} must be a finite number.");
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryReadOptionalPositiveInt(
        IReadOnlyDictionary<string, string> options,
        string optionName,
        out int? value)
    {
        value = null;
        if (!options.TryGetValue(optionName, out var text))
        {
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || parsed < 1)
        {
            WriteFailure(InvalidCliArguments, $"{optionName} must be a positive integer.");
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryReadRequiredPositiveInt(
        IReadOnlyDictionary<string, string> options,
        string optionName,
        string usage,
        out int value)
    {
        value = 0;
        if (!TryReadRequiredOption(options, optionName, usage, out var text))
        {
            return false;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || parsed < 1)
        {
            WriteFailure(InvalidCliArguments, $"{optionName} must be a positive integer.");
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryReadRequiredNonNegativeInt(
        IReadOnlyDictionary<string, string> options,
        string optionName,
        string usage,
        out int value)
    {
        value = 0;
        if (!TryReadRequiredOption(options, optionName, usage, out var text))
        {
            return false;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || parsed < 0)
        {
            WriteFailure(InvalidCliArguments, $"{optionName} must be a non-negative integer.");
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryReadOptionalWatchPaths(
        IReadOnlyDictionary<string, string> options,
        out IReadOnlyList<string>? watchPaths)
    {
        watchPaths = null;
        if (!options.TryGetValue("watch", out var text))
        {
            return true;
        }

        var paths = text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
        if (paths.Length == 0)
        {
            WriteFailure(InvalidCliArguments, "watch must contain at least one path.");
            return false;
        }

        watchPaths = paths;
        return true;
    }

    private static bool TryReadOptionalEnvironmentVariables(
        IReadOnlyDictionary<string, string> options,
        out IReadOnlyDictionary<string, string> environment)
    {
        environment = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!options.TryGetValue("env", out var text) || string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0)
            {
                WriteFailure(InvalidCliArguments, "env must be a semicolon-separated list of KEY=VALUE entries.");
                return false;
            }

            values[token[..separator]] = token[(separator + 1)..];
        }

        environment = values;
        return true;
    }

    private static bool TryReadOptionalTreeKind(
        IReadOnlyDictionary<string, string> options,
        out string treeKind)
    {
        treeKind = TreeKinds.Visual;
        if (!options.TryGetValue("tree-kind", out var value))
        {
            return true;
        }

        if (string.Equals(value, TreeKinds.Visual, StringComparison.OrdinalIgnoreCase))
        {
            treeKind = TreeKinds.Visual;
            return true;
        }

        if (string.Equals(value, TreeKinds.Logical, StringComparison.OrdinalIgnoreCase))
        {
            treeKind = TreeKinds.Logical;
            return true;
        }

        WriteFailure(InvalidCliArguments, "tree-kind must be visual or logical.");
        return false;
    }

    private static bool TryNormalizeInputAction(string action, out string normalizedAction)
    {
        normalizedAction = action;
        foreach (var supportedAction in SupportedInputActions)
        {
            if (string.Equals(action, supportedAction, StringComparison.OrdinalIgnoreCase))
            {
                normalizedAction = supportedAction;
                return true;
            }
        }

        WriteFailure(InvalidCliArguments, $"Unsupported input action '{action}'.");
        return false;
    }

    private static bool ValidateInputActionArguments(
        string action,
        double? x,
        double? y,
        string? inputText,
        string? targetNodeId,
        string? inputKey)
    {
        return action switch
        {
            InputActions.PointerMove or InputActions.PointerDown or InputActions.PointerUp or InputActions.Click
                => RequireCoordinates(action, x, y),
            InputActions.Focus => !string.IsNullOrWhiteSpace(targetNodeId) || RequireCoordinates(action, x, y),
            InputActions.KeyText => RequireText(action, inputText, "text"),
            InputActions.ClearText => true,
            InputActions.KeyDown or InputActions.KeyUp => RequireText(action, inputKey, "key"),
            InputActions.Select => RequireTargetNode(action, targetNodeId) && RequireText(action, inputText, "text"),
            InputActions.Scroll => RequireTargetNode(action, targetNodeId) && RequireAnyCoordinate(action, x, y),
            _ => false
        };
    }

    private static bool IsMutationCliSuccess(RuntimeMutationResponse response)
    {
        return response.Status is RuntimeMutationStatuses.Applied or RuntimeMutationStatuses.NoOp;
    }

    private static RuntimeMutationReviewResponse WithReviewArtifact(
        RuntimeMutationReviewResponse response,
        RuntimeMutationReviewArtifact artifact)
    {
        return new RuntimeMutationReviewResponse(
            response.SessionId,
            response.ReviewedAt,
            response.HistoryCount,
            response.ActiveMutationCount,
            response.History,
            response.ActiveMutations,
            response.ResetHandoff,
            response.Metadata,
            artifact,
            response.SourceContext,
            response.SourceSuggestions);
    }

    private static RuntimeSourceSuggestionContext? CreateSourceSuggestionContext(
        IReadOnlyDictionary<string, string> options,
        string source)
    {
        var context = new RuntimeSourceSuggestionContext(
            options.GetValueOrDefault("source-project"),
            options.GetValueOrDefault("source-view"),
            options.GetValueOrDefault("source-app"),
            options.GetValueOrDefault("source-profile"),
            source);
        return context.HasAnyPath ? context : null;
    }

    private static bool RequireCoordinates(string action, double? x, double? y)
    {
        if (x is not null && y is not null)
        {
            return true;
        }

        WriteFailure(InvalidCliArguments, $"{action} requires x and y coordinates.");
        return false;
    }

    private static bool RequireText(string action, string? value, string optionName)
    {
        if (!string.IsNullOrEmpty(value))
        {
            return true;
        }

        WriteFailure(InvalidCliArguments, $"{action} requires {optionName}.");
        return false;
    }

    private static bool RequireTargetNode(string action, string? targetNodeId)
    {
        if (!string.IsNullOrWhiteSpace(targetNodeId))
        {
            return true;
        }

        WriteFailure(InvalidCliArguments, $"{action} requires target-node.");
        return false;
    }

    private static bool RequireAnyCoordinate(string action, double? x, double? y)
    {
        if (x is not null || y is not null)
        {
            return true;
        }

        WriteFailure(InvalidCliArguments, $"{action} requires x or y delta.");
        return false;
    }

    private static bool TryReadRequiredSessionId(
        IReadOnlyDictionary<string, string> options,
        string usage,
        out SessionId? sessionId)
    {
        sessionId = null;
        if (!TryReadRequiredOption(options, "session", usage, out var sessionText))
        {
            return false;
        }

        try
        {
            sessionId = new SessionId(sessionText!);
            return true;
        }
        catch (ArgumentException exception)
        {
            WriteFailure(CoreErrorCodes.InvalidBridgeRequest, exception.Message);
            return false;
        }
    }

    private static bool TryReadRequiredOption(
        IReadOnlyDictionary<string, string> options,
        string optionName,
        string usage,
        out string? value)
    {
        if (!options.TryGetValue(optionName, out value) || string.IsNullOrWhiteSpace(value))
        {
            value = null;
            WriteFailure(InvalidCliArguments, usage);
            return false;
        }

        return true;
    }

    private static int UnknownCommand(string command)
    {
        WriteFailure(InvalidCliArguments, $"Unknown command '{command}'. {GetUsage()}");
        return 2;
    }

    private static string GetUsage()
    {
        return "Usage: avascope mcp | avascope doctor [--manifest-dir <dir>] [--preview-session-store <dir>] | avascope attach [--latest true|false] [--process <pid>] [--process-name <name>] [--session <session-id>] [--manifest <path>] [--manifest-dir <dir>] | avascope list-top-levels --session <session-id> [--manifest-dir <dir>] | avascope visual-tree --session <session-id> --top-level <top-level-id> [--max-depth <n>] [--manifest-dir <dir>] | avascope logical-tree --session <session-id> --top-level <top-level-id> [--max-depth <n>] [--manifest-dir <dir>] | avascope inspect-node --session <session-id> --top-level <top-level-id> --node <node-id> [--tree-kind visual|logical] [--manifest-dir <dir>] | avascope find-nodes --session <session-id> --top-level <top-level-id> [--tree-kind visual|logical] [--type <type>] [--name <name>] [--automation-id <id>] [--text <text>] [--max-depth <n>] [--max-results <n>] [--manifest-dir <dir>] | avascope input --session <session-id> --top-level <top-level-id> --action <action> [--x <x>] [--y <y>] [--text <text>] [--target-node <node-id>] [--key <key>] [--modifiers <modifiers>] [--manifest-dir <dir>] | avascope mutate-node --session <session-id> --top-level <top-level-id> --node <node-id> --operation <operation> [--tree-kind visual|logical] [--property <name>] [--value <value>] [--value-type <type>] [--class <class>] [--resource-key <key>] [--mutation-id <id>] [--request-id <id>] [--manifest-dir <dir>] | avascope mutate-node-evidence --session <session-id> --top-level <top-level-id> --node <node-id> --operation <operation> --out-dir <dir> [--tree-kind visual|logical] [--property <name>] [--value <value>] [--value-type <type>] [--class <class>] [--resource-key <key>] [--mutation-id <id>] [--request-id <id>] [--max-depth <n>] [--diff true|false] [--tolerance <0-255>] [--manifest-dir <dir>] | avascope mutation-review --session <session-id> [--max-results <n>] [--out <review.html>] [--manifest-dir <dir>] [--source-project <csproj>] [--source-view <view.axaml>] [--source-app <app.axaml>] [--source-profile <profile.json>] | avascope close-session --session <session-id> [--manifest-dir <dir>] | avascope diagnostics [--process <pid>] [--process-name <name>] [--session <session-id>] [--manifest <path>] [--manifest-dir <dir>] [--max-sessions <n>] | avascope launch-app --command <path> [--args <args>] [--env KEY=VALUE[;KEY=VALUE...]] [--timeout-ms <ms>] | avascope reload --session <session-id> [--manifest-dir <dir>] | avascope create-preview-session <project.csproj> [--profile <name>] [--profile-file <path>] [--variant <name>] --view <view.axaml> --out <preview.png> [--width <width>] [--height <height>] [--dpi <dpi>] [--theme light|dark] [--culture <culture>] [--design-data-type <type>] [--display-name <name>] | avascope list-preview-sessions | avascope reload-preview-session --session <session-id> | avascope close-preview-session --session <session-id> | avascope watch-preview-session --session <session-id> --timeout-ms <ms> [--settle-ms <ms>] [--max-reloads <n>] [--watch <path>[,<path>...]] | avascope preview-viewer --session <session-id> [--out <viewer.html>] | avascope baseline-create <project.csproj> --view <view.axaml> --manifest <baseline.json> --sizes <w>x<h>[,<w>x<h>...] [--out-dir <dir>] [--dpi <dpi>] [--theme light|dark] [--culture <culture>] [--design-data-type <type>] | avascope baseline-create --suite <suite.json> --manifest <baseline.json> [--out-dir <dir>] | avascope baseline-check --manifest <baseline.json> [--out-dir <dir>] [--diff-dir <dir>] [--tolerance <0-255>] [--report <report.json>] [--report-pack <dir>] | avascope cleanup | avascope cleanup-bridge-sessions [--manifest-dir <dir>] | avascope diff --baseline <baseline.png> --current <current.png> --out <diff.png> [--tolerance <0-255>] | avascope assert-region --image <image.png> --assert non_empty|mostly_blank|changed|unchanged --x <x> --y <y> --width <w> --height <h> [--baseline <baseline.png>] [--crop-out <crop.png>] [--tolerance <0-255>] [--min-changed-pixels <n>] | avascope screenshot --session <session-id> --top-level <top-level-id> --out <screenshot.png> [--manifest-dir <dir>] | avascope preview-animation <project.csproj> [--profile <name>] [--profile-file <path>] [--variant <name>] --view <view.axaml> --out <frame.png> --time-offsets <ms>[,<ms>...] [--frame-strip <strip.png>] [--viewer <viewer.html>] [--width <width>] [--height <height>] [--dpi <dpi>] [--theme light|dark] [--culture <culture>] [--design-data-type <type>] | avascope preview <project.csproj> [--profile <name>] [--profile-file <path>] [--variant <name>] --view <view.axaml> --out <preview.png> [--width <width>] [--height <height>] [--sizes <w>x<h>[,<w>x<h>...]] [--contact-sheet <sheet.png>] [--dpi <dpi>] [--theme light|dark] [--culture <culture>] [--design-data-type <type>]";
    }

    private static string GetPreviewUsage()
    {
        return "Usage: avascope preview <project.csproj> [--profile <name>] [--profile-file <path>] [--variant <name>] --view <view.axaml> --out <preview.png> [--width <width>] [--height <height>] [--sizes <w>x<h>[,<w>x<h>...]] [--contact-sheet <sheet.png>] [--dpi <dpi>] [--theme light|dark] [--culture <culture>] [--design-data-type <type>]";
    }

    private static string GetPreviewAnimationUsage()
    {
        return "Usage: avascope preview-animation <project.csproj> [--profile <name>] [--profile-file <path>] [--variant <name>] --view <view.axaml> --out <frame.png> --time-offsets <ms>[,<ms>...] [--frame-strip <strip.png>] [--viewer <viewer.html>] [--width <width>] [--height <height>] [--dpi <dpi>] [--theme light|dark] [--culture <culture>] [--design-data-type <type>]";
    }

    private static string GetCreatePreviewSessionUsage()
    {
        return "Usage: avascope create-preview-session <project.csproj> [--profile <name>] [--profile-file <path>] [--variant <name>] --view <view.axaml> --out <preview.png> [--width <width>] [--height <height>] [--dpi <dpi>] [--theme light|dark] [--culture <culture>] [--design-data-type <type>] [--display-name <name>]";
    }

    private static string GetListPreviewSessionsUsage()
    {
        return "Usage: avascope list-preview-sessions";
    }

    private static string GetReloadPreviewSessionUsage()
    {
        return "Usage: avascope reload-preview-session --session <session-id>";
    }

    private static string GetClosePreviewSessionUsage()
    {
        return "Usage: avascope close-preview-session --session <session-id>";
    }

    private static string GetWatchPreviewSessionUsage()
    {
        return "Usage: avascope watch-preview-session --session <session-id> --timeout-ms <ms> [--settle-ms <ms>] [--max-reloads <n>] [--watch <path>[,<path>...]]";
    }

    private static string GetPreviewViewerUsage()
    {
        return "Usage: avascope preview-viewer --session <session-id> [--out <viewer.html>]";
    }

    private static string GetBaselineCreateUsage()
    {
        return "Usage: avascope baseline-create <project.csproj> --view <view.axaml> --manifest <baseline.json> --sizes <w>x<h>[,<w>x<h>...] [--out-dir <dir>] [--dpi <dpi>] [--theme light|dark] [--culture <culture>] [--design-data-type <type>] | avascope baseline-create --suite <suite.json> --manifest <baseline.json> [--out-dir <dir>]";
    }

    private static string GetBaselineCheckUsage()
    {
        return "Usage: avascope baseline-check --manifest <baseline.json> [--out-dir <dir>] [--diff-dir <dir>] [--tolerance <0-255>] [--report <report.json>] [--report-pack <dir>]";
    }

    private static string GetAttachUsage()
    {
        return "Usage: avascope attach [--latest true|false] [--process <pid>] [--process-name <name>] [--session <session-id>] [--manifest <path>] [--manifest-dir <dir>]";
    }

    private static string GetListTopLevelsUsage()
    {
        return "Usage: avascope list-top-levels --session <session-id> [--manifest-dir <dir>]";
    }

    private static string GetVisualTreeUsage()
    {
        return "Usage: avascope visual-tree --session <session-id> --top-level <top-level-id> [--max-depth <n>] [--manifest-dir <dir>]";
    }

    private static string GetLogicalTreeUsage()
    {
        return "Usage: avascope logical-tree --session <session-id> --top-level <top-level-id> [--max-depth <n>] [--manifest-dir <dir>]";
    }

    private static string GetInspectNodeUsage()
    {
        return "Usage: avascope inspect-node --session <session-id> --top-level <top-level-id> --node <node-id> [--tree-kind visual|logical] [--manifest-dir <dir>]";
    }

    private static string GetFindNodesUsage()
    {
        return "Usage: avascope find-nodes --session <session-id> --top-level <top-level-id> [--tree-kind visual|logical] [--type <type>] [--name <name>] [--automation-id <id>] [--text <text>] [--max-depth <n>] [--max-results <n>] [--manifest-dir <dir>]";
    }

    private static string GetInputUsage()
    {
        return "Usage: avascope input --session <session-id> --top-level <top-level-id> --action <action> [--x <x>] [--y <y>] [--text <text>] [--target-node <node-id>] [--key <key>] [--modifiers <modifiers>] [--manifest-dir <dir>]";
    }

    private static string GetMutateNodeUsage()
    {
        return "Usage: avascope mutate-node --session <session-id> --top-level <top-level-id> --node <node-id> --operation <operation> [--tree-kind visual|logical] [--property <name>] [--value <value>] [--value-type <type>] [--class <class>] [--resource-key <key>] [--mutation-id <id>] [--request-id <id>] [--manifest-dir <dir>]";
    }

    private static string GetMutateNodeEvidenceUsage()
    {
        return "Usage: avascope mutate-node-evidence --session <session-id> --top-level <top-level-id> --node <node-id> --operation <operation> --out-dir <dir> [--tree-kind visual|logical] [--property <name>] [--value <value>] [--value-type <type>] [--class <class>] [--resource-key <key>] [--mutation-id <id>] [--request-id <id>] [--max-depth <n>] [--diff true|false] [--tolerance <0-255>] [--manifest-dir <dir>]";
    }

    private static string GetMutationReviewUsage()
    {
        return "Usage: avascope mutation-review --session <session-id> [--max-results <n>] [--out <review.html>] [--manifest-dir <dir>] [--source-project <csproj>] [--source-view <view.axaml>] [--source-app <app.axaml>] [--source-profile <profile.json>]";
    }

    private static string GetCloseSessionUsage()
    {
        return "Usage: avascope close-session --session <session-id> [--manifest-dir <dir>]";
    }

    private static string GetDiagnosticsUsage()
    {
        return "Usage: avascope diagnostics [--process <pid>] [--process-name <name>] [--session <session-id>] [--manifest <path>] [--manifest-dir <dir>] [--max-sessions <n>]";
    }

    private static string GetLaunchAppUsage()
    {
        return "Usage: avascope launch-app --command <path> [--args <args>] [--working-dir <dir>] [--display-name <name>] [--manifest-dir <dir>] [--out-dir <dir>] [--env KEY=VALUE[;KEY=VALUE...]] [--timeout-ms <ms>]";
    }

    private static string GetDoctorUsage()
    {
        return "Usage: avascope doctor [--manifest-dir <dir>] [--preview-session-store <dir>]";
    }

    private static string GetReloadUsage()
    {
        return "Usage: avascope reload --session <session-id> [--manifest-dir <dir>]";
    }

    private static string GetCleanupUsage()
    {
        return "Usage: avascope cleanup";
    }

    private static string GetCleanupBridgeSessionsUsage()
    {
        return "Usage: avascope cleanup-bridge-sessions [--manifest-dir <dir>]";
    }

    private static string GetDiffUsage()
    {
        return "Usage: avascope diff --baseline <baseline.png> --current <current.png> --out <diff.png> [--tolerance <0-255>]";
    }

    private static string GetAssertRegionUsage()
    {
        return "Usage: avascope assert-region --image <image.png> --assert non_empty|mostly_blank|changed|unchanged --x <x> --y <y> --width <w> --height <h> [--baseline <baseline.png>] [--crop-out <crop.png>] [--tolerance <0-255>] [--min-changed-pixels <n>] [--mostly-blank-max-nonblank-percent <0-100>]";
    }

    private static string GetScreenshotUsage()
    {
        return "Usage: avascope screenshot --session <session-id> --top-level <top-level-id> --out <screenshot.png> [--manifest-dir <dir>]";
    }

    private static void WriteFailure(string code, string message)
    {
        WriteResult(ToolResult<PreviewResponse>.Fail(new ProtocolError(code, message)));
    }

    private static void WriteFailure<T>(string code, string message)
    {
        WriteResult(ToolResult<T>.Fail(new ProtocolError(code, message)));
    }

    private static void WriteResult<T>(ToolResult<T> result)
    {
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
    }

    private static void WriteResult<T>(CoreResult<T> result)
    {
        WriteResult(result.Success
            ? ToolResult<T>.Ok(result.Value!)
            : ToolResult<T>.Fail(new ProtocolError(
                result.Error!.Code,
                result.Error.Message,
                result.Error.Details)));
    }

    private static readonly string[] SupportedInputActions =
    [
        InputActions.PointerMove,
        InputActions.PointerDown,
        InputActions.PointerUp,
        InputActions.Click,
        InputActions.Focus,
        InputActions.KeyText,
        InputActions.ClearText,
        InputActions.KeyDown,
        InputActions.KeyUp,
        InputActions.Select,
        InputActions.Scroll
    ];

    private sealed record OptionParseResult(
        bool Success,
        IReadOnlyDictionary<string, string> Values,
        string? Error)
    {
        public static OptionParseResult Ok(IReadOnlyDictionary<string, string> values)
        {
            return new OptionParseResult(true, values, null);
        }

        public static OptionParseResult Fail(string error)
        {
            return new OptionParseResult(false, new Dictionary<string, string>(), error);
        }
    }
}
