using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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
        ApplyCulture(request.Culture);
        var buildError = BuildProject(fullProjectPath);
        if (buildError is not null)
        {
            return ToolResult<PreviewResponse>.Fail(buildError);
        }

        var outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var designData = CreateDesignData(fullProjectPath, request.DesignDataType);
        var projectApplicationScope = LoadProjectApplicationScope(fullProjectPath);
        var content = LoadContent(fullProjectPath, fullViewPath);
        if (designData is not null)
        {
            content.DataContext = designData;
        }

        var window = new Window
        {
            Width = request.Width,
            Height = request.Height,
            Background = Brushes.White,
            Content = content
        };
        foreach (var style in projectApplicationScope.Styles)
        {
            window.Styles.Add(style);
        }
        foreach (var dataTemplate in projectApplicationScope.DataTemplates)
        {
            window.DataTemplates.Add(dataTemplate);
        }

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
            request.ThemeVariant,
            request.Culture,
            request.DesignDataType));
    }

    private static void ApplyCulture(string? cultureName)
    {
        if (cultureName is null)
        {
            return;
        }

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
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

    private static ProtocolError? BuildProject(string? fullProjectPath)
    {
        if (fullProjectPath is null)
        {
            return null;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = Path.GetDirectoryName(fullProjectPath) ?? Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add("build");
        process.StartInfo.ArgumentList.Add(fullProjectPath);
        process.StartInfo.ArgumentList.Add("--nologo");
        process.StartInfo.ArgumentList.Add("--disable-build-servers");

        if (!process.Start())
        {
            return new ProtocolError(
                PreviewHostErrorCodes.ProjectBuildFailed,
                $"Could not start project build for '{fullProjectPath}'.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(milliseconds: 60000))
        {
            process.Kill(entireProcessTree: true);
            return new ProtocolError(
                PreviewHostErrorCodes.ProjectBuildFailed,
                $"Project build timed out for '{fullProjectPath}'.");
        }

        var output = string.Concat(
            stdoutTask.GetAwaiter().GetResult(),
            Environment.NewLine,
            stderrTask.GetAwaiter().GetResult()).Trim();

        if (process.ExitCode == 0)
        {
            return null;
        }

        return new ProtocolError(
            PreviewHostErrorCodes.ProjectBuildFailed,
            TrimBuildOutput(output));
    }

    private static string TrimBuildOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return "Project build failed without output.";
        }

        const int maximumLength = 4000;
        return output.Length <= maximumLength
            ? output
            : output[^maximumLength..];
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

    private static Control LoadContent(string? fullProjectPath, string? fullViewPath)
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

        if (fullProjectPath is not null && TryLoadCompiledProjectView(fullProjectPath, fullViewPath) is { } compiled)
        {
            return compiled;
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

    private static object? CreateDesignData(string? fullProjectPath, string? designDataType)
    {
        if (designDataType is null)
        {
            return null;
        }

        if (fullProjectPath is null)
        {
            throw new ArgumentException("Design data type requires a project path.", nameof(designDataType));
        }

        var projectAssemblyPath = FindProjectAssemblyPath(fullProjectPath)
            ?? throw new ArgumentException("Design data type requires a built project assembly.", nameof(designDataType));
        var assembly = Assembly.LoadFrom(projectAssemblyPath);
        var type = FindDesignDataType(assembly, designDataType);
        if (type is null)
        {
            throw new ArgumentException($"Design data type '{designDataType}' was not found in the preview project assembly.", nameof(designDataType));
        }

        if (type.IsAbstract || type.IsInterface)
        {
            throw new ArgumentException($"Design data type '{designDataType}' must be concrete.", nameof(designDataType));
        }

        if (!type.IsPublic && !type.IsNestedPublic)
        {
            throw new ArgumentException($"Design data type '{designDataType}' must be public.", nameof(designDataType));
        }

        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new ArgumentException($"Design data type '{designDataType}' must have a public parameterless constructor.", nameof(designDataType));
        }

        try
        {
            return Activator.CreateInstance(type);
        }
        catch (Exception exception) when (exception is TargetInvocationException or MemberAccessException)
        {
            throw new InvalidOperationException($"Design data type '{designDataType}' could not be constructed: {exception.Message}", exception);
        }
    }

    private static Type? FindDesignDataType(Assembly assembly, string designDataType)
    {
        var fullNameMatch = assembly.GetType(designDataType, throwOnError: false, ignoreCase: false);
        if (fullNameMatch is not null)
        {
            return fullNameMatch;
        }

        var simpleNameMatches = assembly.GetTypes()
            .Where(type => string.Equals(type.Name, designDataType, StringComparison.Ordinal))
            .ToArray();
        return simpleNameMatches.Length == 1 ? simpleNameMatches[0] : null;
    }

    private static ProjectApplicationScope LoadProjectApplicationScope(string? fullProjectPath)
    {
        if (fullProjectPath is null)
        {
            return ProjectApplicationScope.Empty;
        }

        var projectDirectory = Path.GetDirectoryName(fullProjectPath) ?? Environment.CurrentDirectory;
        var appXamlPath = Path.Combine(projectDirectory, "App.axaml");
        if (!File.Exists(appXamlPath))
        {
            return ProjectApplicationScope.Empty;
        }

        var projectAssemblyPath = FindProjectAssemblyPath(fullProjectPath);
        if (projectAssemblyPath is null)
        {
            return ProjectApplicationScope.Empty;
        }

        var assembly = Assembly.LoadFrom(projectAssemblyPath);
        var appUri = new Uri($"avares://{assembly.GetName().Name}/App.axaml");

        Application projectApplication;
        try
        {
            projectApplication = CreateProjectApplication(assembly)
                ?? AvaloniaXamlLoader.Load(appUri, appUri) as Application
                ?? throw new NotSupportedException("Preview project App.axaml must load to an Avalonia Application.");
        }
        catch (XamlLoadException exception)
        {
            throw new InvalidOperationException($"Preview project App.axaml could not be loaded: {exception.Message}", exception);
        }

        return MergeProjectApplication(projectApplication);
    }

    private static Application? CreateProjectApplication(Assembly assembly)
    {
        var applicationType = assembly
            .GetTypes()
            .FirstOrDefault(static type => typeof(Application).IsAssignableFrom(type)
                && !type.IsAbstract
                && type.GetConstructor(Type.EmptyTypes) is not null);
        if (applicationType is null)
        {
            return null;
        }

        var application = (Application)Activator.CreateInstance(applicationType)!;
        application.Initialize();
        return application;
    }

    private static ProjectApplicationScope MergeProjectApplication(Application projectApplication)
    {
        var hostApplication = Application.Current
            ?? throw new InvalidOperationException("Preview host application was not initialized.");

        // Resource dictionaries are parented by Avalonia, so copy entries instead of reusing the dictionary instance.
        foreach (var resource in projectApplication.Resources.ToArray())
        {
            hostApplication.Resources[resource.Key] = resource.Value;
        }

        foreach (var mergedDictionary in projectApplication.Resources.MergedDictionaries.ToArray())
        {
            projectApplication.Resources.MergedDictionaries.Remove(mergedDictionary);
            hostApplication.Resources.MergedDictionaries.Add(mergedDictionary);
        }

        foreach (var themeDictionary in projectApplication.Resources.ThemeDictionaries.ToArray())
        {
            projectApplication.Resources.ThemeDictionaries.Remove(themeDictionary.Key);
            hostApplication.Resources.ThemeDictionaries[themeDictionary.Key] = themeDictionary.Value;
        }

        var styles = projectApplication.Styles.ToArray();
        foreach (var style in projectApplication.Styles.ToArray())
        {
            projectApplication.Styles.Remove(style);
        }

        var dataTemplates = projectApplication.DataTemplates.ToArray();
        foreach (var dataTemplate in dataTemplates)
        {
            projectApplication.DataTemplates.Remove(dataTemplate);
        }

        return new ProjectApplicationScope(styles, dataTemplates);
    }

    private static Control? TryLoadCompiledProjectView(string fullProjectPath, string fullViewPath)
    {
        var projectAssemblyPath = FindProjectAssemblyPath(fullProjectPath);
        if (projectAssemblyPath is null)
        {
            return null;
        }

        var assembly = Assembly.LoadFrom(projectAssemblyPath);
        var projectDirectory = Path.GetDirectoryName(fullProjectPath) ?? Environment.CurrentDirectory;
        var resourcePath = Path.GetRelativePath(projectDirectory, fullViewPath).Replace('\\', '/');
        var viewUri = new Uri($"avares://{assembly.GetName().Name}/{resourcePath}");

        try
        {
            return AvaloniaXamlLoader.Load(viewUri, viewUri) as Control;
        }
        catch (XamlLoadException)
        {
            return null;
        }
    }

    private static string? FindProjectAssemblyPath(string fullProjectPath)
    {
        var projectDirectory = Path.GetDirectoryName(fullProjectPath);
        if (string.IsNullOrEmpty(projectDirectory))
        {
            return null;
        }

        var assemblyName = Path.GetFileNameWithoutExtension(fullProjectPath);
        var outputRoot = Path.Combine(projectDirectory, "bin", "Debug");
        if (!Directory.Exists(outputRoot))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(outputRoot, $"{assemblyName}.dll", SearchOption.AllDirectories)
            .OrderByDescending(static path => File.GetLastWriteTimeUtc(path))
            .FirstOrDefault();
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

    private sealed record ProjectApplicationScope(
        IReadOnlyList<IStyle> Styles,
        IReadOnlyList<IDataTemplate> DataTemplates)
    {
        public static ProjectApplicationScope Empty { get; } = new([], []);
    }
}
