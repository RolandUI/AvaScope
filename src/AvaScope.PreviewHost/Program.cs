using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaScope.Protocol;

namespace AvaScope.PreviewHost;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> Main(string[] args)
    {
        var requestPath = GetRequestPath(args);
        if (requestPath is null)
        {
            WriteResult(ToolResult<PreviewResponse>.Fail(new ProtocolError(
                PreviewHostErrorCodes.InvalidArguments,
                "Usage: AvaScope.PreviewHost --request <request.json>")));
            return 2;
        }

        try
        {
            var request = await ReadRequestAsync(requestPath);
            BuildAvaloniaApp().SetupWithoutStarting();

            var result = Dispatcher.UIThread.CheckAccess()
                ? Render(request)
                : await Dispatcher.UIThread
                    .InvokeAsync(() => Render(request), DispatcherPriority.Send)
                    .GetTask();

            WriteResult(result);
            return result.Success ? 0 : 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            WriteResult(ToolResult<PreviewResponse>.Fail(new ProtocolError(
                PreviewHostErrorCodes.InvalidRequest,
                exception.Message)));
            return 1;
        }
        catch (Exception exception) when (exception is InvalidOperationException or XamlLoadException or NotSupportedException)
        {
            WriteResult(ToolResult<PreviewResponse>.Fail(new ProtocolError(
                PreviewHostErrorCodes.RenderFailed,
                exception.Message)));
            return 1;
        }
    }

    private static string? GetRequestPath(IReadOnlyList<string> args)
    {
        if (args.Count != 2 || !string.Equals(args[0], "--request", StringComparison.Ordinal))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(args[1]) ? null : args[1];
    }

    private static async Task<PreviewRequest> ReadRequestAsync(string requestPath)
    {
        await using var stream = File.OpenRead(requestPath);
        var request = await JsonSerializer.DeserializeAsync<PreviewRequest>(stream, JsonOptions);

        return request ?? throw new JsonException("Preview request JSON did not contain a request object.");
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<PreviewHostApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            });
    }

    private static ToolResult<PreviewResponse> Render(PreviewRequest request)
    {
        var fullOutputPath = Path.GetFullPath(request.OutputPath);
        var fullProjectPath = ResolveProjectPath(request.ProjectPath);
        var fullViewPath = ResolveViewPath(request.ViewPath, fullProjectPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var window = new Window
        {
            Width = request.Width,
            Height = request.Height,
            Background = Brushes.White,
            Content = LoadContent(fullViewPath)
        };

        window.RequestedThemeVariant = ResolveThemeVariant(request.ThemeVariant);
        window.SetRenderScaling(request.Dpi / 96d);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            return ToolResult<PreviewResponse>.Fail(new ProtocolError(
                PreviewHostErrorCodes.RenderFailed,
                "Preview host did not produce a rendered frame."));
        }

        using (var stream = File.Create(fullOutputPath))
        {
            frame.Save(stream);
        }

        window.Close();

        return ToolResult<PreviewResponse>.Ok(new PreviewResponse(
            fullOutputPath,
            frame.PixelSize.Width,
            frame.PixelSize.Height,
            request.Dpi,
            DateTimeOffset.UtcNow,
            fullProjectPath,
            fullViewPath,
            request.ThemeVariant));
    }

    private static string? ResolveProjectPath(string? projectPath)
    {
        if (projectPath is null)
        {
            return null;
        }

        var fullProjectPath = Path.GetFullPath(projectPath);
        if (!string.Equals(Path.GetExtension(fullProjectPath), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Project path must point to a .csproj file.", nameof(projectPath));
        }

        if (!File.Exists(fullProjectPath))
        {
            throw new FileNotFoundException($"Preview project '{fullProjectPath}' was not found.", fullProjectPath);
        }

        return fullProjectPath;
    }

    private static string? ResolveViewPath(string? viewPath, string? fullProjectPath)
    {
        if (viewPath is null)
        {
            return null;
        }

        if (Path.IsPathRooted(viewPath))
        {
            return Path.GetFullPath(viewPath);
        }

        var baseDirectory = fullProjectPath is null
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(fullProjectPath) ?? Environment.CurrentDirectory;

        return Path.GetFullPath(Path.Combine(baseDirectory, viewPath));
    }

    private static Control LoadContent(string? fullViewPath)
    {
        if (fullViewPath is null)
        {
            return new TextBlock
            {
                Text = "AvaScope preview",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
        }

        if (!File.Exists(fullViewPath))
        {
            throw new FileNotFoundException($"Preview view '{fullViewPath}' was not found.", fullViewPath);
        }

        var viewUri = new Uri(fullViewPath);
        var loaded = AvaloniaRuntimeXamlLoader.Load(
            File.ReadAllText(fullViewPath),
            typeof(Program).Assembly,
            rootInstance: null,
            viewUri,
            designMode: true);
        return loaded as Control
            ?? throw new NotSupportedException("Preview view XAML must load to an Avalonia Control.");
    }

    private static ThemeVariant? ResolveThemeVariant(string? themeVariant)
    {
        if (themeVariant is null)
        {
            return null;
        }

        return themeVariant.ToLowerInvariant() switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => throw new ArgumentException($"Theme variant '{themeVariant}' is not supported.", nameof(themeVariant))
        };
    }

    private static void WriteResult(ToolResult<PreviewResponse> result)
    {
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
    }
}
