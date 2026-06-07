using System.Globalization;
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
            "attach" => await Attach(args[1..]),
            "list-top-levels" => await ListTopLevels(args[1..]),
            "screenshot" => await Screenshot(args[1..]),
            "visual-tree" => await Tree(args[1..], TreeKinds.Visual, GetVisualTreeUsage()),
            "logical-tree" => await Tree(args[1..], TreeKinds.Logical, GetLogicalTreeUsage()),
            "inspect-node" => await InspectNode(args[1..]),
            "find-nodes" => await FindNodes(args[1..]),
            "input" => await Input(args[1..]),
            "close-session" => await CloseSession(args[1..]),
            "diagnostics" => await Diagnostics(args[1..]),
            "reload" => await Reload(args[1..]),
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

        if (!options.Values.TryGetValue("view", out var viewPath)
            || !options.Values.TryGetValue("out", out var outputPath)
            || !options.Values.TryGetValue("width", out var widthText)
            || !options.Values.TryGetValue("height", out var heightText))
        {
            WriteFailure(InvalidCliArguments, GetPreviewUsage());
            return 2;
        }

        if (!TryParsePositiveDouble(widthText, out var width)
            || !TryParsePositiveDouble(heightText, out var height)
            || !TryParsePositiveDouble(options.Values.GetValueOrDefault("dpi", "96"), out var dpi))
        {
            WriteFailure(InvalidCliArguments, "Width, height, and dpi must be positive numbers.");
            return 2;
        }

        PreviewRequest request;
        try
        {
            request = new PreviewRequest(
                outputPath,
                width,
                height,
                dpi,
                projectPath,
                viewPath,
                options.Values.GetValueOrDefault("theme"),
                options.Values.GetValueOrDefault("culture"),
                options.Values.GetValueOrDefault("design-data-type"));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            WriteFailure(CoreErrorCodes.InvalidPreviewRequest, exception.Message);
            return 2;
        }

        var result = await new PreviewHostClient().RenderAsync(request);
        WriteResult(result.Success
            ? ToolResult<PreviewResponse>.Ok(result.Value!)
            : ToolResult<PreviewResponse>.Fail(new ProtocolError(result.Error!.Code, result.Error.Message)));

        return result.Success ? 0 : 1;
    }

    private static async Task<int> Attach(string[] args)
    {
        var options = ParseOptions(args, GetAttachUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetAttachUsage(), "process", "session"))
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

        var result = await new LocalBridgeClient().AttachToAppAsync(processId, sessionId);
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

        if (!ValidateOptions(options.Values, GetListTopLevelsUsage(), "session")
            || !TryReadRequiredSessionId(options.Values, GetListTopLevelsUsage(), out var sessionId))
        {
            return 2;
        }

        var result = await new LocalBridgeClient().ListTopLevelsAsync(sessionId!);
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

        if (!ValidateOptions(options.Values, usage, "session", "top-level", "max-depth")
            || !TryReadRequiredSessionId(options.Values, usage, out var sessionId)
            || !TryReadRequiredOption(options.Values, "top-level", usage, out var topLevelId)
            || !TryReadOptionalNonNegativeInt(options.Values, "max-depth", out var maxDepth))
        {
            return 2;
        }

        var client = new LocalBridgeClient();
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

        if (!ValidateOptions(options.Values, GetInspectNodeUsage(), "session", "top-level", "node", "tree-kind")
            || !TryReadRequiredSessionId(options.Values, GetInspectNodeUsage(), out var sessionId)
            || !TryReadRequiredOption(options.Values, "top-level", GetInspectNodeUsage(), out var topLevelId)
            || !TryReadRequiredOption(options.Values, "node", GetInspectNodeUsage(), out var nodeId)
            || !TryReadOptionalTreeKind(options.Values, out var treeKind))
        {
            return 2;
        }

        var result = await new LocalBridgeClient().InspectNodeAsync(sessionId!, topLevelId!, treeKind, nodeId!);
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
                "max-results")
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

        var result = await new LocalBridgeClient().FindNodesAsync(
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
                "modifiers")
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

        var result = await new LocalBridgeClient().InputAsync(
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

    private static async Task<int> Reload(string[] args)
    {
        var options = ParseOptions(args, GetReloadUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetReloadUsage(), "session")
            || !TryReadRequiredSessionId(options.Values, GetReloadUsage(), out var sessionId))
        {
            return 2;
        }

        var result = await new LocalBridgeClient().ReloadRuntimeAsync(sessionId!);
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

        if (!ValidateOptions(options.Values, GetDiagnosticsUsage(), "process", "session", "max-sessions")
            || !TryReadOptionalProcessId(options.Values, out var processId)
            || !TryReadOptionalSessionId(options.Values, out var sessionId)
            || !TryReadOptionalDiagnosticsMaxSessions(options.Values, out var maxSessions))
        {
            return 2;
        }

        var result = await new LocalBridgeClient().DiagnosticsAsync(
            processId,
            sessionId,
            maxSessions,
            new PreviewHostClient().GetDiagnostics());
        WriteResult(result);

        return result.Success ? 0 : 1;
    }

    private static async Task<int> CloseSession(string[] args)
    {
        var options = ParseOptions(args, GetCloseSessionUsage());
        if (!options.Success)
        {
            WriteFailure(InvalidCliArguments, options.Error!);
            return 2;
        }

        if (!ValidateOptions(options.Values, GetCloseSessionUsage(), "session")
            || !TryReadRequiredSessionId(options.Values, GetCloseSessionUsage(), out var sessionId))
        {
            return 2;
        }

        var result = await new LocalBridgeClient().CloseSessionAsync(sessionId!);
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

        if (!ValidateOptions(options.Values, GetScreenshotUsage(), "session", "top-level", "out")
            || !TryReadRequiredSessionId(options.Values, GetScreenshotUsage(), out var sessionId)
            || !TryReadRequiredOption(options.Values, "top-level", GetScreenshotUsage(), out var topLevelId)
            || !TryReadRequiredOption(options.Values, "out", GetScreenshotUsage(), out var outputPath))
        {
            return 2;
        }

        var result = await new LocalBridgeClient().CaptureScreenshotAsync(sessionId!, topLevelId!, outputPath!);
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
            InputActions.KeyDown or InputActions.KeyUp => RequireText(action, inputKey, "key"),
            _ => false
        };
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
        return "Usage: avascope mcp | avascope attach [--process <pid>] [--session <session-id>] | avascope list-top-levels --session <session-id> | avascope visual-tree --session <session-id> --top-level <top-level-id> [--max-depth <n>] | avascope logical-tree --session <session-id> --top-level <top-level-id> [--max-depth <n>] | avascope inspect-node --session <session-id> --top-level <top-level-id> --node <node-id> [--tree-kind visual|logical] | avascope find-nodes --session <session-id> --top-level <top-level-id> [--tree-kind visual|logical] [--type <type>] [--name <name>] [--automation-id <id>] [--text <text>] [--max-depth <n>] [--max-results <n>] | avascope input --session <session-id> --top-level <top-level-id> --action <action> [--x <x>] [--y <y>] [--text <text>] [--target-node <node-id>] [--key <key>] [--modifiers <modifiers>] | avascope close-session --session <session-id> | avascope diagnostics [--process <pid>] [--session <session-id>] [--max-sessions <n>] | avascope reload --session <session-id> | avascope screenshot --session <session-id> --top-level <top-level-id> --out <screenshot.png> | avascope preview <project.csproj> --view <view.axaml> --out <preview.png> --width <width> --height <height> [--dpi <dpi>] [--theme light|dark] [--culture <culture>] [--design-data-type <type>]";
    }

    private static string GetPreviewUsage()
    {
        return "Usage: avascope preview <project.csproj> --view <view.axaml> --out <preview.png> --width <width> --height <height> [--dpi <dpi>] [--theme light|dark] [--culture <culture>] [--design-data-type <type>]";
    }

    private static string GetAttachUsage()
    {
        return "Usage: avascope attach [--process <pid>] [--session <session-id>]";
    }

    private static string GetListTopLevelsUsage()
    {
        return "Usage: avascope list-top-levels --session <session-id>";
    }

    private static string GetVisualTreeUsage()
    {
        return "Usage: avascope visual-tree --session <session-id> --top-level <top-level-id> [--max-depth <n>]";
    }

    private static string GetLogicalTreeUsage()
    {
        return "Usage: avascope logical-tree --session <session-id> --top-level <top-level-id> [--max-depth <n>]";
    }

    private static string GetInspectNodeUsage()
    {
        return "Usage: avascope inspect-node --session <session-id> --top-level <top-level-id> --node <node-id> [--tree-kind visual|logical]";
    }

    private static string GetFindNodesUsage()
    {
        return "Usage: avascope find-nodes --session <session-id> --top-level <top-level-id> [--tree-kind visual|logical] [--type <type>] [--name <name>] [--automation-id <id>] [--text <text>] [--max-depth <n>] [--max-results <n>]";
    }

    private static string GetInputUsage()
    {
        return "Usage: avascope input --session <session-id> --top-level <top-level-id> --action <action> [--x <x>] [--y <y>] [--text <text>] [--target-node <node-id>] [--key <key>] [--modifiers <modifiers>]";
    }

    private static string GetCloseSessionUsage()
    {
        return "Usage: avascope close-session --session <session-id>";
    }

    private static string GetDiagnosticsUsage()
    {
        return "Usage: avascope diagnostics [--process <pid>] [--session <session-id>] [--max-sessions <n>]";
    }

    private static string GetReloadUsage()
    {
        return "Usage: avascope reload --session <session-id>";
    }

    private static string GetScreenshotUsage()
    {
        return "Usage: avascope screenshot --session <session-id> --top-level <top-level-id> --out <screenshot.png>";
    }

    private static void WriteFailure(string code, string message)
    {
        WriteResult(ToolResult<PreviewResponse>.Fail(new ProtocolError(code, message)));
    }

    private static void WriteResult<T>(ToolResult<T> result)
    {
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
    }

    private static void WriteResult<T>(CoreResult<T> result)
    {
        WriteResult(result.Success
            ? ToolResult<T>.Ok(result.Value!)
            : ToolResult<T>.Fail(new ProtocolError(result.Error!.Code, result.Error.Message)));
    }

    private static readonly string[] SupportedInputActions =
    [
        InputActions.PointerMove,
        InputActions.PointerDown,
        InputActions.PointerUp,
        InputActions.Click,
        InputActions.Focus,
        InputActions.KeyText,
        InputActions.KeyDown,
        InputActions.KeyUp
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
