using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
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
    private const string BlendDesignNamespace = "http://schemas.microsoft.com/expression/blend/2008";

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
                exception.Message,
                CreateExceptionDetails("request", exception))));
            return 1;
        }
        catch (Exception exception) when (exception is InvalidOperationException or XamlLoadException or NotSupportedException)
        {
            WriteResult(ToolResult<PreviewResponse>.Fail(new ProtocolError(
                PreviewHostErrorCodes.RenderFailed,
                exception.Message,
                CreateExceptionDetails("render", exception))));
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

        var sourceMetadataResult = LoadPreviewSourceMetadata(
            fullProjectPath,
            fullViewPath,
            fullOutputPath);
        if (!sourceMetadataResult.Success)
        {
            return ToolResult<PreviewResponse>.Fail(sourceMetadataResult.Error!);
        }

        var sourceMetadata = sourceMetadataResult.Value!;
        var dimensionsResult = ResolvePreviewDimensions(
            request,
            sourceMetadata,
            fullProjectPath,
            fullViewPath,
            fullOutputPath);
        if (!dimensionsResult.Success)
        {
            return ToolResult<PreviewResponse>.Fail(dimensionsResult.Error!);
        }

        var dimensions = dimensionsResult.Value!;
        var designData = CreateDesignData(fullProjectPath, request.DesignDataType);
        var projectApplicationScope = LoadProjectApplicationScope(fullProjectPath);
        var content = LoadContent(fullProjectPath, fullViewPath);
        if (designData is not null)
        {
            content.DataContext = designData;
        }
        else
        {
            var designTimeDataContextResult = ResolveDesignTimeDataContext(
                fullProjectPath,
                fullViewPath,
                fullOutputPath,
                content,
                sourceMetadata);
            if (!designTimeDataContextResult.Success)
            {
                return ToolResult<PreviewResponse>.Fail(designTimeDataContextResult.Error!);
            }

            var designTimeDataContext = designTimeDataContextResult.Value!;
            if (designTimeDataContext.HasValue)
            {
                content.DataContext = designTimeDataContext.Value;
            }
        }

        var window = CreateRenderWindow(
            content,
            dimensions,
            projectApplicationScope,
            ResolveThemeVariant(request.ThemeVariant),
            request.Dpi);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            using var frame = window.CaptureRenderedFrame();
            if (frame is null)
            {
                return ToolResult<PreviewResponse>.Fail(new ProtocolError(
                    PreviewHostErrorCodes.RenderFailed,
                    "Preview host did not produce a rendered frame.",
                    CreateRenderDetails(fullProjectPath, fullViewPath, fullOutputPath)));
            }

            using (var stream = File.Create(fullOutputPath))
            {
                frame.Save(stream);
            }

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
        finally
        {
            window.Close();
        }
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

        var workingDirectory = Path.GetDirectoryName(fullProjectPath) ?? Environment.CurrentDirectory;
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
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
            return CreateProjectBuildError(
                $"Could not start project build for '{fullProjectPath}'.",
                fullProjectPath,
                workingDirectory);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(milliseconds: 60000))
        {
            process.Kill(entireProcessTree: true);
            return CreateProjectBuildError(
                $"Project build timed out for '{fullProjectPath}'.",
                fullProjectPath,
                workingDirectory,
                timeoutMilliseconds: 60000);
        }

        var output = string.Concat(
            stdoutTask.GetAwaiter().GetResult(),
            Environment.NewLine,
            stderrTask.GetAwaiter().GetResult()).Trim();

        if (process.ExitCode == 0)
        {
            return null;
        }

        var outputTail = TrimBuildOutput(output);
        return CreateProjectBuildError(
            outputTail,
            fullProjectPath,
            workingDirectory,
            exitCode: process.ExitCode,
            outputTail: outputTail);
    }

    private static ProtocolError CreateProjectBuildError(
        string message,
        string fullProjectPath,
        string workingDirectory,
        int? exitCode = null,
        string? outputTail = null,
        int? timeoutMilliseconds = null)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phase"] = "build",
            ["projectPath"] = fullProjectPath,
            ["workingDirectory"] = workingDirectory,
            ["command"] = $"dotnet build \"{fullProjectPath}\" --nologo --disable-build-servers"
        };

        if (exitCode is not null)
        {
            details["exitCode"] = exitCode.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (timeoutMilliseconds is not null)
        {
            details["timeoutMilliseconds"] = timeoutMilliseconds.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(outputTail))
        {
            details["outputTail"] = outputTail;
        }

        return new ProtocolError(PreviewHostErrorCodes.ProjectBuildFailed, message, details);
    }

    private static IReadOnlyDictionary<string, string> CreateRenderDetails(
        string? fullProjectPath,
        string? fullViewPath,
        string fullOutputPath)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phase"] = "render",
            ["outputPath"] = fullOutputPath
        };

        if (fullProjectPath is not null)
        {
            details["projectPath"] = fullProjectPath;
        }

        if (fullViewPath is not null)
        {
            details["viewPath"] = fullViewPath;
        }

        return details;
    }

    private static IReadOnlyDictionary<string, string> CreateExceptionDetails(
        string phase,
        Exception exception)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phase"] = phase,
            ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name
        };
    }

    private static IReadOnlyDictionary<string, string> CreateDesignMetadataDetails(
        string? fullProjectPath,
        string? fullViewPath,
        string fullOutputPath,
        string? expression,
        Exception exception)
    {
        var details = new Dictionary<string, string>(
            CreateRenderDetails(fullProjectPath, fullViewPath, fullOutputPath),
            StringComparer.Ordinal)
        {
            ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name
        };

        if (!string.IsNullOrWhiteSpace(expression))
        {
            details["designDataContext"] = expression;
        }

        return details;
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

    private static ToolResult<PreviewSourceMetadata> LoadPreviewSourceMetadata(
        string? fullProjectPath,
        string? fullViewPath,
        string fullOutputPath)
    {
        if (fullViewPath is null || !File.Exists(fullViewPath))
        {
            return ToolResult<PreviewSourceMetadata>.Ok(PreviewSourceMetadata.Empty);
        }

        try
        {
            using var stream = File.OpenRead(fullViewPath);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit
            });
            var document = XDocument.Load(reader);
            var root = document.Root;
            if (root is null)
            {
                return ToolResult<PreviewSourceMetadata>.Ok(PreviewSourceMetadata.Empty);
            }

            var namespaces = ReadNamespaceDeclarations(root);
            var designWidth = ReadDesignDimension(root, "DesignWidth", "Design.Width");
            var designHeight = ReadDesignDimension(root, "DesignHeight", "Design.Height");
            var designDataContext = root
                .Attribute(XName.Get("DataContext", BlendDesignNamespace))
                ?.Value;
            var designDataContextObject = ReadDesignDataContextObjectElement(root);

            return ToolResult<PreviewSourceMetadata>.Ok(new PreviewSourceMetadata(
                designWidth,
                designHeight,
                string.IsNullOrWhiteSpace(designDataContext) ? null : designDataContext,
                designDataContextObject,
                namespaces));
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException)
        {
            return ToolResult<PreviewSourceMetadata>.Fail(new ProtocolError(
                PreviewHostErrorCodes.RenderFailed,
                $"Preview design metadata could not be loaded: {exception.Message}",
                CreateDesignMetadataDetails(fullProjectPath, fullViewPath, fullOutputPath, null, exception)));
        }
    }

    private static IReadOnlyDictionary<string, string> ReadNamespaceDeclarations(XElement root)
    {
        var namespaces = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in root.Attributes().Where(static attribute => attribute.IsNamespaceDeclaration))
        {
            var prefix = attribute.Name.LocalName == "xmlns"
                ? string.Empty
                : attribute.Name.LocalName;
            namespaces[prefix] = attribute.Value;
        }

        return namespaces;
    }

    private static double? ReadDesignDimension(
        XElement root,
        string blendName,
        string attachedName)
    {
        var text = root.Attribute(XName.Get(blendName, BlendDesignNamespace))?.Value
            ?? root
                .Attributes()
                .FirstOrDefault(attribute => string.Equals(
                    attribute.Name.LocalName,
                    attachedName,
                    StringComparison.Ordinal))
                ?.Value;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || value < 1)
        {
            throw new InvalidOperationException($"Design-time dimension '{blendName}' must be a positive number.");
        }

        return value;
    }

    private static string? ReadDesignDataContextObjectElement(XElement root)
    {
        var dataContextElement = root
            .Elements()
            .FirstOrDefault(static element => string.Equals(
                element.Name.LocalName,
                "Design.DataContext",
                StringComparison.Ordinal));
        return dataContextElement
            ?.Elements()
            .FirstOrDefault()
            ?.ToString(SaveOptions.DisableFormatting);
    }

    private static ToolResult<PreviewDimensions> ResolvePreviewDimensions(
        PreviewRequest request,
        PreviewSourceMetadata sourceMetadata,
        string? fullProjectPath,
        string? fullViewPath,
        string fullOutputPath)
    {
        var width = request.Width ?? sourceMetadata.DesignWidth;
        var height = request.Height ?? sourceMetadata.DesignHeight;
        if (width is null || height is null)
        {
            return ToolResult<PreviewDimensions>.Fail(new ProtocolError(
                PreviewHostErrorCodes.InvalidRequest,
                "Preview width and height are required unless the view declares design-time width and height.",
                CreateRenderDetails(fullProjectPath, fullViewPath, fullOutputPath)));
        }

        return ToolResult<PreviewDimensions>.Ok(new PreviewDimensions(width.Value, height.Value));
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

    private static Window CreateRenderWindow(
        Control content,
        PreviewDimensions dimensions,
        ProjectApplicationScope projectApplicationScope,
        ThemeVariant? themeVariant,
        double dpi)
    {
        var window = content is Window rootWindow
            ? rootWindow
            : new Window
            {
                Background = Brushes.White,
                Content = content
            };

        window.Width = dimensions.Width;
        window.Height = dimensions.Height;
        if (window.Background is null)
        {
            window.Background = Brushes.White;
        }

        foreach (var style in projectApplicationScope.Styles)
        {
            window.Styles.Add(style);
        }

        foreach (var dataTemplate in projectApplicationScope.DataTemplates)
        {
            window.DataTemplates.Add(dataTemplate);
        }

        window.RequestedThemeVariant = themeVariant;
        window.SetRenderScaling(dpi / 96d);
        return window;
    }

    private static ToolResult<DesignDataResolution> ResolveDesignTimeDataContext(
        string? fullProjectPath,
        string? fullViewPath,
        string fullOutputPath,
        Control content,
        PreviewSourceMetadata sourceMetadata)
    {
        var attachedDesignDataContext = Design.GetDataContext(content);
        if (attachedDesignDataContext is not null)
        {
            return ToolResult<DesignDataResolution>.Ok(DesignDataResolution.FromValue(attachedDesignDataContext));
        }

        if (sourceMetadata.DesignDataContextExpression is null)
        {
            if (sourceMetadata.DesignDataContextObjectElement is null)
            {
                return ToolResult<DesignDataResolution>.Ok(DesignDataResolution.None);
            }

            if (fullProjectPath is null)
            {
                return ToolResult<DesignDataResolution>.Fail(new ProtocolError(
                    PreviewHostErrorCodes.RenderFailed,
                    "Design-time DataContext requires a preview project path.",
                    CreateRenderDetails(fullProjectPath, fullViewPath, fullOutputPath)));
            }

            try
            {
                return ToolResult<DesignDataResolution>.Ok(DesignDataResolution.FromValue(
                    LoadDesignDataContextObject(fullProjectPath, fullViewPath, sourceMetadata.DesignDataContextObjectElement)));
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or NotSupportedException
                or XamlLoadException
                or TargetInvocationException
                or TypeLoadException
                or FileLoadException
                or BadImageFormatException)
            {
                return ToolResult<DesignDataResolution>.Fail(new ProtocolError(
                    PreviewHostErrorCodes.RenderFailed,
                    $"Design-time DataContext could not be loaded: {exception.Message}",
                    CreateDesignMetadataDetails(
                        fullProjectPath,
                        fullViewPath,
                        fullOutputPath,
                        sourceMetadata.DesignDataContextObjectElement,
                        exception)));
            }
        }

        if (fullProjectPath is null)
        {
            return ToolResult<DesignDataResolution>.Fail(new ProtocolError(
                PreviewHostErrorCodes.RenderFailed,
                "Design-time DataContext requires a preview project path.",
                CreateRenderDetails(fullProjectPath, fullViewPath, fullOutputPath)));
        }

        try
        {
            return ToolResult<DesignDataResolution>.Ok(DesignDataResolution.FromValue(
                ResolveStaticDesignDataContext(
                    fullProjectPath,
                    sourceMetadata.DesignDataContextExpression,
                    sourceMetadata.Namespaces)));
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or TargetInvocationException
            or TypeLoadException
            or FileLoadException
            or BadImageFormatException)
        {
            return ToolResult<DesignDataResolution>.Fail(new ProtocolError(
                PreviewHostErrorCodes.RenderFailed,
                $"Design-time DataContext could not be resolved: {exception.Message}",
                CreateDesignMetadataDetails(
                    fullProjectPath,
                    fullViewPath,
                    fullOutputPath,
                    sourceMetadata.DesignDataContextExpression,
                    exception)));
        }
    }

    private static object? LoadDesignDataContextObject(
        string fullProjectPath,
        string? fullViewPath,
        string objectElementXaml)
    {
        var projectAssemblyPath = FindProjectAssemblyPath(fullProjectPath)
            ?? throw new InvalidOperationException("Design-time DataContext requires a built project assembly.");
        var assembly = Assembly.LoadFrom(projectAssemblyPath);
        var viewUri = fullViewPath is null
            ? new Uri(Path.GetDirectoryName(fullProjectPath) ?? Environment.CurrentDirectory)
            : new Uri(fullViewPath);

        return AvaloniaRuntimeXamlLoader.Load(
            objectElementXaml,
            assembly,
            rootInstance: null,
            viewUri,
            designMode: true);
    }

    private static object? ResolveStaticDesignDataContext(
        string fullProjectPath,
        string expression,
        IReadOnlyDictionary<string, string> namespaces)
    {
        var staticMember = ExtractXStaticMember(expression)
            ?? throw new NotSupportedException(
                "Only d:DataContext values using '{x:Static prefix:Type.Member}' are supported.");

        var projectAssemblyPath = FindProjectAssemblyPath(fullProjectPath)
            ?? throw new InvalidOperationException("Design-time DataContext requires a built project assembly.");
        var projectAssembly = Assembly.LoadFrom(projectAssemblyPath);
        var memberReference = ParseStaticMemberReference(staticMember);
        var type = ResolveXamlType(projectAssembly, memberReference.TypeName, memberReference.Prefix, namespaces)
            ?? throw new InvalidOperationException($"Design data type '{memberReference.TypeName}' was not found.");

        const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        var property = type.GetProperty(memberReference.MemberName, bindingFlags);
        if (property is not null)
        {
            return property.GetValue(null);
        }

        var field = type.GetField(memberReference.MemberName, bindingFlags);
        if (field is not null)
        {
            return field.GetValue(null);
        }

        throw new InvalidOperationException(
            $"Static design data member '{memberReference.MemberName}' was not found on type '{type.FullName}'.");
    }

    private static string? ExtractXStaticMember(string expression)
    {
        var trimmed = expression.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[^1] != '}')
        {
            return null;
        }

        var body = trimmed[1..^1].Trim();
        const string directive = "x:Static";
        if (!body.StartsWith(directive, StringComparison.Ordinal))
        {
            return null;
        }

        var argument = body[directive.Length..].Trim();
        const string memberPrefix = "Member=";
        if (argument.StartsWith(memberPrefix, StringComparison.Ordinal))
        {
            argument = argument[memberPrefix.Length..].Trim();
        }

        argument = argument.Trim('"', '\'');
        return string.IsNullOrWhiteSpace(argument) ? null : argument;
    }

    private static StaticMemberReference ParseStaticMemberReference(string staticMember)
    {
        var prefixEnd = staticMember.IndexOf(':');
        string? prefix = null;
        var typeAndMember = staticMember;
        if (prefixEnd >= 0)
        {
            prefix = staticMember[..prefixEnd];
            typeAndMember = staticMember[(prefixEnd + 1)..];
        }

        var memberSeparator = typeAndMember.LastIndexOf('.');
        if (memberSeparator <= 0 || memberSeparator == typeAndMember.Length - 1)
        {
            throw new NotSupportedException(
                "x:Static design data references must use a Type.Member shape.");
        }

        return new StaticMemberReference(
            prefix,
            typeAndMember[..memberSeparator],
            typeAndMember[(memberSeparator + 1)..]);
    }

    private static Type? ResolveXamlType(
        Assembly projectAssembly,
        string xamlTypeName,
        string? prefix,
        IReadOnlyDictionary<string, string> namespaces)
    {
        Assembly assembly = projectAssembly;
        var typeName = xamlTypeName;
        if (prefix is not null)
        {
            if (!namespaces.TryGetValue(prefix, out var namespaceValue))
            {
                throw new InvalidOperationException($"XAML namespace prefix '{prefix}' was not declared.");
            }

            var namespaceReference = ParseXamlNamespace(namespaceValue);
            assembly = ResolveReferencedAssembly(projectAssembly, namespaceReference.AssemblyName);
            typeName = string.IsNullOrWhiteSpace(namespaceReference.ClrNamespace)
                ? xamlTypeName
                : $"{namespaceReference.ClrNamespace}.{xamlTypeName}";
        }

        return assembly.GetType(typeName, throwOnError: false, ignoreCase: false)
            ?? assembly
                .GetTypes()
                .FirstOrDefault(type => string.Equals(type.FullName, typeName, StringComparison.Ordinal));
    }

    private static XamlNamespaceReference ParseXamlNamespace(string value)
    {
        const string usingPrefix = "using:";
        if (value.StartsWith(usingPrefix, StringComparison.Ordinal))
        {
            return new XamlNamespaceReference(value[usingPrefix.Length..], null);
        }

        const string clrNamespacePrefix = "clr-namespace:";
        if (value.StartsWith(clrNamespacePrefix, StringComparison.Ordinal))
        {
            var namespaceParts = value[clrNamespacePrefix.Length..].Split(';');
            var clrNamespace = namespaceParts[0];
            string? assemblyName = null;
            foreach (var part in namespaceParts.Skip(1))
            {
                const string assemblyPrefix = "assembly=";
                if (part.StartsWith(assemblyPrefix, StringComparison.Ordinal))
                {
                    assemblyName = part[assemblyPrefix.Length..];
                    break;
                }
            }

            return new XamlNamespaceReference(clrNamespace, assemblyName);
        }

        throw new NotSupportedException($"XAML namespace '{value}' is not supported for design data resolution.");
    }

    private static Assembly ResolveReferencedAssembly(Assembly projectAssembly, string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName)
            || string.Equals(projectAssembly.GetName().Name, assemblyName, StringComparison.Ordinal))
        {
            return projectAssembly;
        }

        var loadedAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(
                assembly.GetName().Name,
                assemblyName,
                StringComparison.Ordinal));
        if (loadedAssembly is not null)
        {
            return loadedAssembly;
        }

        return Assembly.Load(new AssemblyName(assemblyName));
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

    private sealed record PreviewSourceMetadata(
        double? DesignWidth,
        double? DesignHeight,
        string? DesignDataContextExpression,
        string? DesignDataContextObjectElement,
        IReadOnlyDictionary<string, string> Namespaces)
    {
        public static PreviewSourceMetadata Empty { get; } = new(
            null,
            null,
            null,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private sealed record PreviewDimensions(double Width, double Height);

    private sealed record DesignDataResolution(bool HasValue, object? Value)
    {
        public static DesignDataResolution None { get; } = new(false, null);

        public static DesignDataResolution FromValue(object? value) => new(true, value);
    }

    private sealed record StaticMemberReference(string? Prefix, string TypeName, string MemberName);

    private sealed record XamlNamespaceReference(string ClrNamespace, string? AssemblyName);
}
