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
        var options = ParseOptions(args[1..]);
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
                options.Values.GetValueOrDefault("theme"));
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

    private static OptionParseResult ParseOptions(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Count; index += 2)
        {
            var key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Count)
            {
                return OptionParseResult.Fail(GetPreviewUsage());
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

    private static int UnknownCommand(string command)
    {
        WriteFailure(InvalidCliArguments, $"Unknown command '{command}'. {GetUsage()}");
        return 2;
    }

    private static string GetUsage()
    {
        return "Usage: avascope mcp | avascope preview <project.csproj> --view <view.axaml> --out <preview.png> --width <width> --height <height> [--dpi <dpi>] [--theme light|dark]";
    }

    private static string GetPreviewUsage()
    {
        return "Usage: avascope preview <project.csproj> --view <view.axaml> --out <preview.png> --width <width> --height <height> [--dpi <dpi>] [--theme light|dark]";
    }

    private static void WriteFailure(string code, string message)
    {
        WriteResult(ToolResult<PreviewResponse>.Fail(new ProtocolError(code, message)));
    }

    private static void WriteResult(ToolResult<PreviewResponse> result)
    {
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
    }

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
