using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Protocol;

namespace AvaScope.PreviewHost;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string BlendDesignNamespace = "http://schemas.microsoft.com/expression/blend/2008";
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const int MaximumPreviewDiagnostics = 100;
    private const double MinimumHitTargetSize = 24;
    private const double TextLayoutWidthTolerance = 1;
    private const double TextLayoutHeightTolerance = 4;
    private static readonly string[] PreviewBackgroundResourceKeys =
    [
        "SystemRegionBrush",
        "SystemControlPageBackgroundChromeLowBrush",
        "SystemControlPageBackgroundChromeMediumLowBrush",
        "SystemControlBackgroundChromeLowBrush",
        "SystemControlBackgroundChromeMediumLowBrush",
        "SystemControlPageBackgroundAltHighBrush",
        "SystemControlBackgroundAltHighBrush",
        "SystemRegionColor"
    ];

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
        var pathsResult = ResolvePreviewPaths(request);
        if (!pathsResult.Success)
        {
            return ToolResult<PreviewResponse>.Fail(pathsResult.Error!);
        }

        var paths = pathsResult.Value!;
        var fullOutputPath = paths.OutputPath;
        var fullProjectPath = paths.ProjectPath;
        var fullViewPath = paths.ViewPath;
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
        var diagnostics = new List<PreviewDiagnostic>();
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
            else if (content.DataContext is null && projectApplicationScope.HasDataContext)
            {
                content.DataContext = projectApplicationScope.DataContext;
            }
        }

        var resolvedThemeVariant = ResolveThemeVariant(request.ThemeVariant);
        var window = CreateRenderWindow(
            content,
            dimensions,
            projectApplicationScope,
            resolvedThemeVariant,
            request.Dpi);
        try
        {
            AddSourceDiagnostics(diagnostics, sourceMetadata, content, window, resolvedThemeVariant, fullProjectPath);
            window.Show();
            Dispatcher.UIThread.RunJobs();
            EnsurePreviewBackground(content, window, resolvedThemeVariant);
            Dispatcher.UIThread.RunJobs();
            AddAnimationSamplingDiagnostic(diagnostics, request.AnimationTimeOffsetMs);
            AdvanceAnimationOffset(request.AnimationTimeOffsetMs);
            AddLayoutDiagnostics(diagnostics, window);

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
                request.DesignDataType,
                diagnostics,
                request.AnimationTimeOffsetMs));
        }
        finally
        {
            window.Close();
        }
    }

    private static void AddAnimationSamplingDiagnostic(List<PreviewDiagnostic> diagnostics, int? timeOffsetMs)
    {
        if (timeOffsetMs is null)
        {
            return;
        }

        AddDiagnostic(diagnostics, new PreviewDiagnostic(
            PreviewDiagnosticSeverities.Info,
            PreviewDiagnosticCategories.Animation,
            "animation_frame_sampled",
            "PreviewHost captured an explicit animation time-offset frame.",
            details: new Dictionary<string, string>
            {
                ["timeOffsetMs"] = timeOffsetMs.Value.ToString(CultureInfo.InvariantCulture),
                ["headlessRenderTicks"] = CalculateHeadlessRenderTicks(timeOffsetMs.Value).ToString(CultureInfo.InvariantCulture),
                ["timeControl"] = "headless_render_timer_tick"
            }));
    }

    private static void AdvanceAnimationOffset(int? timeOffsetMs)
    {
        if (timeOffsetMs is null)
        {
            return;
        }

        Dispatcher.UIThread.RunJobs();
        var renderTicks = CalculateHeadlessRenderTicks(timeOffsetMs.Value);
        if (renderTicks > 0)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(renderTicks);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static int CalculateHeadlessRenderTicks(int timeOffsetMs)
    {
        if (timeOffsetMs <= 0)
        {
            return 0;
        }

        const double frameDurationMs = 1000d / 60d;
        return Math.Max(1, (int)Math.Ceiling(timeOffsetMs / frameDurationMs));
    }

    private static void AddSourceDiagnostics(
        List<PreviewDiagnostic> diagnostics,
        PreviewSourceMetadata sourceMetadata,
        Control content,
        Window window,
        ThemeVariant? themeVariant,
        string? fullProjectPath)
    {
        var dataContext = content.DataContext;
        var projectAssembly = TryLoadProjectAssembly(fullProjectPath);

        foreach (var binding in sourceMetadata.BindingReferences)
        {
            if (!string.IsNullOrWhiteSpace(binding.ConverterResourceKey))
            {
                if (!TryResolveResource(content, window, themeVariant, binding.ConverterResourceKey, out var converter))
                {
                    AddDiagnostic(diagnostics, new PreviewDiagnostic(
                        PreviewDiagnosticSeverities.Warning,
                        PreviewDiagnosticCategories.Binding,
                        "binding_converter_resource_not_found",
                        $"Binding converter resource '{binding.ConverterResourceKey}' could not be resolved.",
                        propertyName: binding.TargetProperty,
                        sourcePath: binding.SourcePath,
                        details: CreateBindingDetails(binding)));
                }
                else if (converter is not IValueConverter)
                {
                    AddDiagnostic(diagnostics, new PreviewDiagnostic(
                        PreviewDiagnosticSeverities.Error,
                        PreviewDiagnosticCategories.Binding,
                        "binding_converter_resource_invalid",
                        $"Binding converter resource '{binding.ConverterResourceKey}' is not an IValueConverter.",
                        propertyName: binding.TargetProperty,
                        sourcePath: binding.SourcePath,
                        details: CreateBindingDetails(
                            binding,
                            new Dictionary<string, string>
                            {
                                ["converterType"] = converter?.GetType().FullName ?? "null"
                            })));
                }
            }

            if (binding.HasExplicitSource)
            {
                continue;
            }

            if (binding.IsCompiledBinding && string.IsNullOrWhiteSpace(binding.DataTypeName))
            {
                AddDiagnostic(diagnostics, new PreviewDiagnostic(
                    PreviewDiagnosticSeverities.Warning,
                    PreviewDiagnosticCategories.Binding,
                    "compiled_binding_missing_datatype",
                    "CompiledBinding was found without an inherited x:DataType in the preview source.",
                    propertyName: binding.TargetProperty,
                    sourcePath: binding.SourcePath,
                    details: CreateBindingDetails(binding)));
            }

            if (string.IsNullOrWhiteSpace(binding.BindingPath))
            {
                continue;
            }

            if (AddDataTypeBindingDiagnostics(diagnostics, sourceMetadata, binding, projectAssembly) != BindingDataTypeDiagnosticResult.NotApplicable)
            {
                continue;
            }

            if (dataContext is null)
            {
                AddDiagnostic(diagnostics, new PreviewDiagnostic(
                    PreviewDiagnosticSeverities.Warning,
                    PreviewDiagnosticCategories.Binding,
                    "binding_missing_datacontext",
                    $"Binding path '{binding.BindingPath}' has no root DataContext in the preview.",
                    propertyName: binding.TargetProperty,
                    sourcePath: binding.SourcePath,
                    details: CreateBindingDetails(binding)));
                continue;
            }

            if (IsInspectableBindingPath(binding.BindingPath)
                && !CanResolveBindingPath(dataContext.GetType(), binding.BindingPath))
            {
                AddDiagnostic(diagnostics, new PreviewDiagnostic(
                    PreviewDiagnosticSeverities.Warning,
                    PreviewDiagnosticCategories.Binding,
                    "binding_path_not_found",
                    $"Binding path '{binding.BindingPath}' was not found on preview DataContext '{dataContext.GetType().FullName}'.",
                    propertyName: binding.TargetProperty,
                    sourcePath: binding.SourcePath,
                    details: CreateBindingDetails(
                        binding,
                        new Dictionary<string, string>
                        {
                            ["dataContextType"] = dataContext.GetType().FullName ?? dataContext.GetType().Name
                        })));
            }
        }

        foreach (var resource in sourceMetadata.ResourceReferences)
        {
            if (TryResolveResource(content, window, themeVariant, resource.Key, out _))
            {
                continue;
            }

            AddDiagnostic(diagnostics, new PreviewDiagnostic(
                PreviewDiagnosticSeverities.Warning,
                PreviewDiagnosticCategories.Resource,
                "resource_not_found",
                $"{resource.Kind} resource '{resource.Key}' could not be resolved in the preview scope.",
                propertyName: resource.TargetProperty,
                sourcePath: resource.SourcePath,
                details: new Dictionary<string, string>
                {
                    ["elementPath"] = resource.ElementPath,
                    ["elementType"] = resource.ElementType,
                    ["resourceKey"] = resource.Key,
                    ["resourceKind"] = resource.Kind
                }));
        }
    }

    private static void AddLayoutDiagnostics(List<PreviewDiagnostic> diagnostics, Window window)
    {
        var rootBounds = GetGlobalBounds(window);
        var visuals = EnumerateVisuals(window).ToArray();

        foreach (var visual in visuals)
        {
            AddTextLayoutDiagnostics(diagnostics, visual);
            AddHitTargetDiagnostics(diagnostics, visual);
            AddClipDiagnostics(diagnostics, visual);

            if (rootBounds is { } root
                && visual != window
                && GetGlobalBounds(visual) is { } bounds
                && !Contains(root, bounds))
            {
                AddDiagnostic(diagnostics, CreateLayoutDiagnostic(
                    "content_unreachable",
                    "Content extends outside the preview root bounds.",
                    visual,
                    bounds));
            }
        }

        foreach (var visual in visuals)
        {
            AddOverlapDiagnostics(diagnostics, visual);
        }
    }

    private static void AddTextLayoutDiagnostics(List<PreviewDiagnostic> diagnostics, Visual visual)
    {
        switch (visual)
        {
            case TextBlock textBlock when !string.IsNullOrEmpty(textBlock.Text):
                AddTextLayoutDiagnostics(
                    diagnostics,
                    textBlock,
                    textBlock.DesiredSize,
                    textBlock.Bounds,
                    textBlock.TextTrimming != TextTrimming.None);
                break;
            case TextBox textBox when !string.IsNullOrEmpty(textBox.Text):
                AddTextLayoutDiagnostics(diagnostics, textBox, textBox.DesiredSize, textBox.Bounds, hasTrimming: false);
                break;
        }
    }

    private static void AddTextLayoutDiagnostics(
        List<PreviewDiagnostic> diagnostics,
        Visual visual,
        Size desiredSize,
        Rect bounds,
        bool hasTrimming)
    {
        if (desiredSize.Width <= bounds.Width + TextLayoutWidthTolerance
            && desiredSize.Height <= bounds.Height + TextLayoutHeightTolerance)
        {
            return;
        }

        var globalBounds = GetGlobalBounds(visual) ?? new Rect(bounds.Size);
        AddDiagnostic(diagnostics, CreateLayoutDiagnostic(
            hasTrimming ? "text_truncated" : "text_clipped",
            hasTrimming
                ? "Text is likely truncated by the rendered bounds."
                : "Text desired size exceeds the rendered bounds.",
            visual,
            globalBounds,
            new Dictionary<string, string>
            {
                ["desiredWidth"] = desiredSize.Width.ToString(CultureInfo.InvariantCulture),
                ["desiredHeight"] = desiredSize.Height.ToString(CultureInfo.InvariantCulture),
                ["boundsWidth"] = bounds.Width.ToString(CultureInfo.InvariantCulture),
                ["boundsHeight"] = bounds.Height.ToString(CultureInfo.InvariantCulture)
            }));
    }

    private static void AddHitTargetDiagnostics(List<PreviewDiagnostic> diagnostics, Visual visual)
    {
        if (visual is not Button and not TextBox)
        {
            return;
        }

        if (visual is RepeatButton && HasVisualAncestor<Slider>(visual))
        {
            return;
        }

        var bounds = GetGlobalBounds(visual);
        if (bounds is null
            || bounds.Value.Width <= 0
            || bounds.Value.Height <= 0
            || (bounds.Value.Width >= MinimumHitTargetSize && bounds.Value.Height >= MinimumHitTargetSize))
        {
            return;
        }

        AddDiagnostic(diagnostics, CreateLayoutDiagnostic(
            "hit_target_too_small",
            $"Interactive target is smaller than the {MinimumHitTargetSize.ToString(CultureInfo.InvariantCulture)}x{MinimumHitTargetSize.ToString(CultureInfo.InvariantCulture)} policy.",
            visual,
            bounds.Value,
            new Dictionary<string, string>
            {
                ["minimumWidth"] = MinimumHitTargetSize.ToString(CultureInfo.InvariantCulture),
                ["minimumHeight"] = MinimumHitTargetSize.ToString(CultureInfo.InvariantCulture)
            }));
    }

    private static void AddClipDiagnostics(List<PreviewDiagnostic> diagnostics, Visual visual)
    {
        if (!visual.ClipToBounds)
        {
            return;
        }

        var parentBounds = GetGlobalBounds(visual);
        if (parentBounds is null)
        {
            return;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            var childBounds = GetGlobalBounds(child);
            if (childBounds is null || Contains(parentBounds.Value, childBounds.Value))
            {
                continue;
            }

            AddDiagnostic(diagnostics, CreateLayoutDiagnostic(
                "content_clipped",
                "Child content extends beyond a clipping parent.",
                child,
                childBounds.Value,
                new Dictionary<string, string>
                {
                    ["parentNodeId"] = CreateNodeId(visual),
                    ["parentType"] = visual.GetType().FullName ?? visual.GetType().Name
                }));
        }
    }

    private static void AddOverlapDiagnostics(List<PreviewDiagnostic> diagnostics, Visual parent)
    {
        if (IsIntentionalOverlayHost(parent) || IsFrameworkTemplateOverlapScope(parent))
        {
            return;
        }

        var children = parent.GetVisualChildren()
            .Where(static child => child.IsVisible)
            .Select(static child => new
            {
                Visual = child,
                Bounds = GetGlobalBounds(child)
            })
            .Where(static item => item.Bounds is { Width: > 1, Height: > 1 })
            .ToArray();

        for (var outer = 0; outer < children.Length; outer++)
        {
            for (var inner = outer + 1; inner < children.Length; inner++)
            {
                var intersection = Intersect(children[outer].Bounds!.Value, children[inner].Bounds!.Value);
                if (intersection is null || Area(intersection.Value) < 4)
                {
                    continue;
                }

                if (ShouldIgnoreOverlap(parent, children[outer].Visual, children[inner].Visual))
                {
                    continue;
                }

                AddDiagnostic(diagnostics, CreateLayoutDiagnostic(
                    "elements_overlap",
                    "Sibling elements overlap in the rendered layout.",
                    children[inner].Visual,
                    intersection.Value,
                    new Dictionary<string, string>
                    {
                        ["firstNodeId"] = CreateNodeId(children[outer].Visual),
                        ["firstType"] = children[outer].Visual.GetType().FullName ?? children[outer].Visual.GetType().Name,
                        ["secondNodeId"] = CreateNodeId(children[inner].Visual),
                        ["secondType"] = children[inner].Visual.GetType().FullName ?? children[inner].Visual.GetType().Name,
                        ["parentNodeId"] = CreateNodeId(parent)
                    }));
            }
        }
    }

    private static bool TryResolveResource(
        Control content,
        Window window,
        ThemeVariant? requestedTheme,
        string key,
        out object? value)
    {
        var theme = requestedTheme ?? window.ActualThemeVariant ?? ThemeVariant.Default;
        if (content.TryGetResource(key, theme, out value)
            || window.TryGetResource(key, theme, out value))
        {
            return true;
        }

        if (Application.Current is { } application && application.TryGetResource(key, theme, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static BindingDataTypeDiagnosticResult AddDataTypeBindingDiagnostics(
        List<PreviewDiagnostic> diagnostics,
        PreviewSourceMetadata sourceMetadata,
        SourceBindingReference binding,
        Assembly? projectAssembly)
    {
        if (projectAssembly is null
            || string.IsNullOrWhiteSpace(binding.DataTypeName)
            || string.IsNullOrWhiteSpace(binding.BindingPath))
        {
            return BindingDataTypeDiagnosticResult.NotApplicable;
        }

        if (!TryResolveBindingDataType(
            projectAssembly,
            binding.DataTypeName,
            binding.DataTypeNamespaces ?? sourceMetadata.Namespaces,
            out var dataType,
            out var resolutionError))
        {
            AddDiagnostic(diagnostics, new PreviewDiagnostic(
                PreviewDiagnosticSeverities.Warning,
                PreviewDiagnosticCategories.Binding,
                "binding_datatype_not_resolved",
                $"x:DataType '{binding.DataTypeName}' could not be resolved for binding diagnostics.",
                propertyName: binding.TargetProperty,
                sourcePath: binding.SourcePath,
                details: CreateBindingDetails(
                    binding,
                    new Dictionary<string, string>
                    {
                        ["resolutionError"] = resolutionError ?? "unknown"
                    })));
            return BindingDataTypeDiagnosticResult.DiagnosticAdded;
        }

        if (dataType is null
            || !IsInspectableBindingPath(binding.BindingPath)
            || CanResolveBindingPath(dataType, binding.BindingPath))
        {
            return BindingDataTypeDiagnosticResult.Checked;
        }

        AddDiagnostic(diagnostics, new PreviewDiagnostic(
            PreviewDiagnosticSeverities.Warning,
            PreviewDiagnosticCategories.Binding,
            "binding_datatype_path_not_found",
            $"Binding path '{binding.BindingPath}' was not found on declared x:DataType '{dataType.FullName}'.",
            propertyName: binding.TargetProperty,
            sourcePath: binding.SourcePath,
            details: CreateBindingDetails(
                binding,
                new Dictionary<string, string>
                {
                    ["dataType"] = dataType.FullName ?? dataType.Name
                })));
        return BindingDataTypeDiagnosticResult.DiagnosticAdded;
    }

    private static bool TryResolveBindingDataType(
        Assembly projectAssembly,
        string dataTypeName,
        IReadOnlyDictionary<string, string> namespaces,
        out Type? dataType,
        out string? resolutionError)
    {
        dataType = null;
        resolutionError = null;

        var reference = ParseXamlTypeReference(dataTypeName);
        if (reference is null)
        {
            resolutionError = "unsupported_xaml_datatype";
            return false;
        }

        try
        {
            dataType = ResolveXamlType(projectAssembly, reference.Value.TypeName, reference.Value.Prefix, namespaces);
            if (dataType is null)
            {
                resolutionError = "type_not_found";
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or TypeLoadException
            or ReflectionTypeLoadException
            or FileLoadException
            or BadImageFormatException)
        {
            resolutionError = exception.Message;
            return false;
        }
    }

    private static (string? Prefix, string TypeName)? ParseXamlTypeReference(string dataTypeName)
    {
        var trimmed = dataTypeName.Trim();
        if (trimmed.Length == 0
            || string.Equals(trimmed, "{x:Null}", StringComparison.Ordinal)
            || string.Equals(trimmed, "x:Null", StringComparison.Ordinal))
        {
            return null;
        }

        const string xTypePrefix = "{x:Type ";
        if (trimmed.StartsWith(xTypePrefix, StringComparison.Ordinal) && trimmed.EndsWith('}'))
        {
            trimmed = trimmed[xTypePrefix.Length..^1].Trim();
        }

        var prefixEnd = trimmed.IndexOf(':');
        if (prefixEnd <= 0)
        {
            return (null, trimmed);
        }

        var prefix = trimmed[..prefixEnd];
        if (string.Equals(prefix, "x", StringComparison.Ordinal))
        {
            return null;
        }

        return (prefix, trimmed[(prefixEnd + 1)..]);
    }

    private static Assembly? TryLoadProjectAssembly(string? fullProjectPath)
    {
        if (fullProjectPath is null)
        {
            return null;
        }

        try
        {
            var projectAssemblyPath = FindProjectAssemblyPath(fullProjectPath);
            if (projectAssemblyPath is null)
            {
                return null;
            }

            var fullAssemblyPath = Path.GetFullPath(projectAssemblyPath);
            var loadedAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(
                    assembly.Location,
                    fullAssemblyPath,
                    StringComparison.OrdinalIgnoreCase));
            return loadedAssembly ?? Assembly.LoadFrom(fullAssemblyPath);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or FileLoadException
            or BadImageFormatException
            or IOException
            or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsInspectableBindingPath(string bindingPath)
    {
        var path = bindingPath.Trim();
        return path.Length > 0
            && !string.Equals(path, ".", StringComparison.Ordinal)
            && !path.StartsWith("(", StringComparison.Ordinal)
            && !path.Contains('[', StringComparison.Ordinal)
            && !path.Contains('/', StringComparison.Ordinal);
    }

    private static bool CanResolveBindingPath(Type dataContextType, string bindingPath)
    {
        var current = dataContextType;
        foreach (var segment in bindingPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var memberName = segment.TrimEnd('?', '!');
            if (memberName.Length == 0)
            {
                return false;
            }

            var property = current.GetProperty(
                memberName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (property is not null)
            {
                current = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                continue;
            }

            var field = current.GetField(
                memberName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (field is null)
            {
                return false;
            }

            current = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
        }

        return true;
    }

    private static IReadOnlyDictionary<string, string> CreateSourceDetails(
        string elementPath,
        string expression,
        IReadOnlyDictionary<string, string>? extra = null)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["elementPath"] = elementPath,
            ["expression"] = expression
        };

        if (extra is not null)
        {
            foreach (var item in extra)
            {
                details[item.Key] = item.Value;
            }
        }

        return details;
    }

    private static IReadOnlyDictionary<string, string> CreateBindingDetails(
        SourceBindingReference binding,
        IReadOnlyDictionary<string, string>? extra = null)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["elementType"] = binding.ElementType,
            ["bindingKind"] = binding.IsCompiledBinding ? "compiled" : "runtime"
        };

        if (!string.IsNullOrWhiteSpace(binding.BindingPath))
        {
            details["bindingPath"] = binding.BindingPath;
        }

        if (!string.IsNullOrWhiteSpace(binding.DataTypeName))
        {
            details["dataTypeName"] = binding.DataTypeName;
        }

        if (!string.IsNullOrWhiteSpace(binding.DataTypePath))
        {
            details["dataTypePath"] = binding.DataTypePath;
        }

        if (extra is not null)
        {
            foreach (var item in extra)
            {
                details[item.Key] = item.Value;
            }
        }

        return CreateSourceDetails(binding.ElementPath, binding.Expression, details);
    }

    private static IEnumerable<Visual> EnumerateVisuals(Visual root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
        {
            foreach (var descendant in EnumerateVisuals(child))
            {
                yield return descendant;
            }
        }
    }

    private static PreviewDiagnostic CreateLayoutDiagnostic(
        string code,
        string message,
        Visual visual,
        Rect bounds,
        IReadOnlyDictionary<string, string>? details = null)
    {
        return new PreviewDiagnostic(
            PreviewDiagnosticSeverities.Warning,
            PreviewDiagnosticCategories.Layout,
            code,
            message,
            CreateNodeId(visual),
            visual.GetType().FullName ?? visual.GetType().Name,
            bounds: new NodeBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            details: details);
    }

    private static void AddDiagnostic(List<PreviewDiagnostic> diagnostics, PreviewDiagnostic diagnostic)
    {
        if (diagnostics.Count < MaximumPreviewDiagnostics)
        {
            diagnostics.Add(diagnostic);
        }
    }

    private static string CreateNodeId(object node)
    {
        return $"{TreeKinds.Visual}:{RuntimeHelpers.GetHashCode(node):x}";
    }

    private static Rect? GetGlobalBounds(Visual visual)
    {
        var transformedBounds = visual.GetTransformedBounds();
        if (transformedBounds is null)
        {
            return null;
        }

        return transformedBounds.Value.Bounds.TransformToAABB(transformedBounds.Value.Transform);
    }

    private static bool Contains(Rect outer, Rect inner)
    {
        return inner.X >= outer.X - 0.5
            && inner.Y >= outer.Y - 0.5
            && inner.Right <= outer.Right + 0.5
            && inner.Bottom <= outer.Bottom + 0.5;
    }

    private static Rect? Intersect(Rect first, Rect second)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min(first.Right, second.Right);
        var bottom = Math.Min(first.Bottom, second.Bottom);
        return right <= left || bottom <= top
            ? null
            : new Rect(left, top, right - left, bottom - top);
    }

    private static double Area(Rect rect)
    {
        return rect.Width * rect.Height;
    }

    private static bool IsIntentionalOverlayHost(Visual visual)
    {
        var typeName = visual.GetType().Name;
        return visual is Canvas
            || typeName.Contains("Overlay", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("Adorner", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("Popup", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldIgnoreOverlap(Visual parent, Visual first, Visual second)
    {
        if (IsFrameworkTemplateOverlapScope(first) || IsFrameworkTemplateOverlapScope(second))
        {
            return true;
        }

        var firstTemplatedParent = first.TemplatedParent;
        var secondTemplatedParent = second.TemplatedParent;
        if (firstTemplatedParent is not null && ReferenceEquals(firstTemplatedParent, secondTemplatedParent))
        {
            return true;
        }

        return parent.TemplatedParent is not null
            && (first.TemplatedParent is not null || second.TemplatedParent is not null);
    }

    private static bool IsFrameworkTemplateOverlapScope(Visual visual)
    {
        var typeName = visual.GetType().Name;
        var fullName = visual.GetType().FullName ?? typeName;
        return visual is Window
            || visual is Viewbox
            || typeName is "ContentPresenter"
            || string.Equals(fullName, "Avalonia.Controls.Primitives.VisualLayerManager", StringComparison.Ordinal)
            || string.Equals(typeName, "VisualLayerManager", StringComparison.Ordinal);
    }

    private static bool HasVisualAncestor<T>(Visual visual)
        where T : Visual
    {
        var current = visual.GetVisualParent();
        while (current is not null)
        {
            if (current is T)
            {
                return true;
            }

            current = current.GetVisualParent();
        }

        return false;
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

    private static ToolResult<PreviewPaths> ResolvePreviewPaths(PreviewRequest request)
    {
        string fullOutputPath;
        string? fullProjectPath;
        string? fullViewPath;
        try
        {
            fullOutputPath = Path.GetFullPath(request.OutputPath);
            fullProjectPath = ResolveProjectPath(request.ProjectPath);
            fullViewPath = ResolveViewPath(request.ViewPath, fullProjectPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ToolResult<PreviewPaths>.Fail(new ProtocolError(
                PreviewHostErrorCodes.InvalidRequest,
                exception.Message,
                CreateExceptionDetails("request", exception)));
        }

        if (fullProjectPath is not null && !File.Exists(fullProjectPath))
        {
            return ToolResult<PreviewPaths>.Fail(CreateReadinessError(
                "project_file",
                $"Preview project '{fullProjectPath}' was not found.",
                fullProjectPath,
                fullViewPath,
                fullOutputPath));
        }

        if (fullViewPath is not null && !File.Exists(fullViewPath))
        {
            return ToolResult<PreviewPaths>.Fail(CreateReadinessError(
                "view_file",
                $"Preview view '{fullViewPath}' was not found.",
                fullProjectPath,
                fullViewPath,
                fullOutputPath));
        }

        return ToolResult<PreviewPaths>.Ok(new PreviewPaths(fullOutputPath, fullProjectPath, fullViewPath));
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

        try
        {
            if (!process.Start())
            {
                return CreateReadinessError(
                    "dotnet_cli",
                    $"Could not start project build for '{fullProjectPath}'.",
                    fullProjectPath,
                    null,
                    string.Empty,
                    workingDirectory,
                    command: $"dotnet build \"{fullProjectPath}\" --nologo --disable-build-servers");
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return CreateReadinessError(
                "dotnet_cli",
                $"Could not start project build for '{fullProjectPath}': {exception.Message}",
                fullProjectPath,
                null,
                string.Empty,
                workingDirectory,
                command: $"dotnet build \"{fullProjectPath}\" --nologo --disable-build-servers",
                exception: exception);
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
            ["command"] = $"dotnet build \"{fullProjectPath}\" --nologo --disable-build-servers",
            ["nextAction"] = "Fix the project build output shown in outputTail, then retry the preview command."
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
            ["outputPath"] = fullOutputPath,
            ["nextAction"] = "Inspect the render exception and view resources, then retry after the view can load in an isolated preview host."
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
            ["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name,
            ["nextAction"] = phase == "request"
                ? "Check the preview request JSON and supplied paths before retrying."
                : "Check the preview host error details before retrying."
        };
    }

    private static ProtocolError CreateReadinessError(
        string requirement,
        string message,
        string? fullProjectPath,
        string? fullViewPath,
        string fullOutputPath,
        string? workingDirectory = null,
        string? command = null,
        Exception? exception = null)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phase"] = "readiness",
            ["requirement"] = requirement,
            ["nextAction"] = requirement switch
            {
                "project_file" => "Pass an existing .csproj path or omit projectPath for standalone AXAML preview.",
                "view_file" => "Pass an existing .axaml view path, relative to the project directory when projectPath is set.",
                "dotnet_cli" => "Install a compatible .NET SDK/runtime and ensure the dotnet executable is on PATH.",
                _ => "Fix the reported local prerequisite before retrying the preview command."
            }
        };

        if (!string.IsNullOrWhiteSpace(fullOutputPath))
        {
            details["outputPath"] = fullOutputPath;
        }

        if (fullProjectPath is not null)
        {
            details["projectPath"] = fullProjectPath;
        }

        if (fullViewPath is not null)
        {
            details["viewPath"] = fullViewPath;
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            details["workingDirectory"] = workingDirectory;
        }

        if (!string.IsNullOrWhiteSpace(command))
        {
            details["command"] = command;
        }

        if (exception is not null)
        {
            details["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name;
        }

        return new ProtocolError(PreviewHostErrorCodes.ReadinessFailed, message, details);
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
            var bindingReferences = ReadBindingReferences(root, fullViewPath);
            var resourceReferences = ReadResourceReferences(root, fullViewPath);

            return ToolResult<PreviewSourceMetadata>.Ok(new PreviewSourceMetadata(
                designWidth,
                designHeight,
                string.IsNullOrWhiteSpace(designDataContext) ? null : designDataContext,
                designDataContextObject,
                namespaces,
                bindingReferences,
                resourceReferences));
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

    private static IReadOnlyList<SourceBindingReference> ReadBindingReferences(XElement root, string? sourcePath)
    {
        var references = new List<SourceBindingReference>();
        foreach (var (element, path, dataType) in EnumerateElementsWithPathsAndDataTypes(root))
        {
            foreach (var attribute in element.Attributes())
            {
                var value = attribute.Value;
                if (!ContainsBindingExpression(value))
                {
                    continue;
                }

                references.Add(new SourceBindingReference(
                    path,
                    element.Name.LocalName,
                    attribute.Name.LocalName,
                    value,
                    ExtractBindingPath(value),
                    ExtractConverterResourceKey(value),
                    ContainsCompiledBindingExpression(value),
                    dataType?.TypeName,
                    dataType?.ElementPath,
                    dataType?.Namespaces,
                    HasExplicitBindingSource(value),
                    sourcePath));

                if (references.Count >= MaximumPreviewDiagnostics)
                {
                    return references;
                }
            }
        }

        return references;
    }

    private static IEnumerable<(XElement Element, string Path, SourceDataTypeReference? DataType)> EnumerateElementsWithPathsAndDataTypes(XElement root)
    {
        return EnumerateElementsWithPathsAndDataTypes(
            root,
            root.Name.LocalName,
            inheritedDataType: null,
            inheritedNamespaces: new Dictionary<string, string>(StringComparer.Ordinal));

        static IEnumerable<(XElement Element, string Path, SourceDataTypeReference? DataType)> EnumerateElementsWithPathsAndDataTypes(
            XElement element,
            string path,
            SourceDataTypeReference? inheritedDataType,
            IReadOnlyDictionary<string, string> inheritedNamespaces)
        {
            var namespaces = MergeNamespaceDeclarations(inheritedNamespaces, element);
            var currentDataType = ReadDataTypeReference(element, path, namespaces) ?? inheritedDataType;
            yield return (element, path, currentDataType);

            var groupedChildren = element.Elements()
                .GroupBy(static child => child.Name.LocalName, StringComparer.Ordinal);
            foreach (var group in groupedChildren)
            {
                var index = 0;
                foreach (var child in group)
                {
                    index++;
                    foreach (var item in EnumerateElementsWithPathsAndDataTypes(
                        child,
                        $"{path}/{child.Name.LocalName}[{index}]",
                        currentDataType,
                        namespaces))
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, string> MergeNamespaceDeclarations(
        IReadOnlyDictionary<string, string> inheritedNamespaces,
        XElement element)
    {
        var declaredNamespaces = element
            .Attributes()
            .Where(static attribute => attribute.IsNamespaceDeclaration)
            .ToArray();
        if (declaredNamespaces.Length == 0)
        {
            return inheritedNamespaces;
        }

        var namespaces = new Dictionary<string, string>(inheritedNamespaces, StringComparer.Ordinal);
        foreach (var attribute in declaredNamespaces)
        {
            var prefix = attribute.Name.LocalName == "xmlns"
                ? string.Empty
                : attribute.Name.LocalName;
            namespaces[prefix] = attribute.Value;
        }

        return namespaces;
    }

    private static SourceDataTypeReference? ReadDataTypeReference(
        XElement element,
        string elementPath,
        IReadOnlyDictionary<string, string> namespaces)
    {
        var attribute = element.Attribute(XName.Get("DataType", XamlNamespace));
        var value = attribute?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new SourceDataTypeReference(value.Trim(), elementPath, namespaces);
    }

    private static IReadOnlyList<SourceResourceReference> ReadResourceReferences(XElement root, string? sourcePath)
    {
        var references = new List<SourceResourceReference>();
        foreach (var (element, path) in EnumerateElementsWithPaths(root))
        {
            foreach (var attribute in element.Attributes())
            {
                foreach (Match match in Regex.Matches(
                    attribute.Value,
                    @"\{(?<kind>StaticResource|DynamicResource)\s+(?<key>[^},\s]+)",
                    RegexOptions.CultureInvariant))
                {
                    references.Add(new SourceResourceReference(
                        path,
                        element.Name.LocalName,
                        attribute.Name.LocalName,
                        match.Groups["key"].Value,
                        match.Groups["kind"].Value,
                        sourcePath));

                    if (references.Count >= MaximumPreviewDiagnostics)
                    {
                        return references;
                    }
                }
            }
        }

        return references;
    }

    private static IEnumerable<(XElement Element, string Path)> EnumerateElementsWithPaths(XElement root)
    {
        return EnumerateElementsWithPaths(root, root.Name.LocalName);

        static IEnumerable<(XElement Element, string Path)> EnumerateElementsWithPaths(
            XElement element,
            string path)
        {
            yield return (element, path);

            var groupedChildren = element.Elements()
                .GroupBy(static child => child.Name.LocalName, StringComparer.Ordinal);
            foreach (var group in groupedChildren)
            {
                var index = 0;
                foreach (var child in group)
                {
                    index++;
                    foreach (var item in EnumerateElementsWithPaths(child, $"{path}/{child.Name.LocalName}[{index}]"))
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    private static bool ContainsBindingExpression(string value)
    {
        return value.Contains("{Binding", StringComparison.Ordinal)
            || value.Contains("{CompiledBinding", StringComparison.Ordinal);
    }

    private static bool ContainsCompiledBindingExpression(string value)
    {
        return value.Contains("{CompiledBinding", StringComparison.Ordinal);
    }

    private static string? ExtractBindingPath(string value)
    {
        var pathMatch = Regex.Match(
            value,
            @"(?:^|[,{]\s*)Path\s*=\s*(?<path>[^,}]+)",
            RegexOptions.CultureInvariant);
        if (pathMatch.Success)
        {
            return CleanBindingToken(pathMatch.Groups["path"].Value);
        }

        var positionalMatch = Regex.Match(
            value,
            @"\{(?:Binding|CompiledBinding)\s+(?<path>[^,}]+)",
            RegexOptions.CultureInvariant);
        if (!positionalMatch.Success)
        {
            return null;
        }

        var path = CleanBindingToken(positionalMatch.Groups["path"].Value);
        return string.Equals(path, "}", StringComparison.Ordinal) ? null : path;
    }

    private static string CleanBindingToken(string value)
    {
        return value.Trim().Trim('"', '\'');
    }

    private static string? ExtractConverterResourceKey(string value)
    {
        var match = Regex.Match(
            value,
            @"Converter\s*=\s*\{(?:StaticResource|DynamicResource)\s+(?<key>[^},\s]+)",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["key"].Value : null;
    }

    private static bool HasExplicitBindingSource(string value)
    {
        return Regex.IsMatch(
            value,
            @"(?:^|[,{]\s*)(Source|ElementName|RelativeSource)\s*=",
            RegexOptions.CultureInvariant);
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
                Content = content
            };

        window.Width = dimensions.Width;
        window.Height = dimensions.Height;

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

    private static void EnsurePreviewBackground(
        Control content,
        Window window,
        ThemeVariant? requestedTheme)
    {
        if (window.Background is not null)
        {
            return;
        }

        window.Background = ResolvePreviewBackground(content, window, requestedTheme);
    }

    private static IBrush ResolvePreviewBackground(
        Control content,
        Window window,
        ThemeVariant? requestedTheme)
    {
        foreach (var key in PreviewBackgroundResourceKeys)
        {
            if (TryResolveResource(content, window, requestedTheme, key, out var value)
                && TryConvertPreviewBackground(value, out var brush))
            {
                return brush;
            }
        }

        return IsDarkTheme(requestedTheme, window) ? Brushes.Black : Brushes.White;
    }

    private static bool TryConvertPreviewBackground(object? value, out IBrush brush)
    {
        switch (value)
        {
            case IBrush resolvedBrush:
                brush = resolvedBrush;
                return true;
            case Color color:
                brush = new SolidColorBrush(color);
                return true;
            default:
                brush = Brushes.Transparent;
                return false;
        }
    }

    private static bool IsDarkTheme(ThemeVariant? requestedTheme, Window window)
    {
        var theme = requestedTheme
            ?? window.ActualThemeVariant
            ?? Application.Current?.ActualThemeVariant
            ?? ThemeVariant.Default;
        return theme == ThemeVariant.Dark;
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

        return new ProjectApplicationScope(
            styles,
            dataTemplates,
            projectApplication.DataContext is not null,
            projectApplication.DataContext);
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

    private enum BindingDataTypeDiagnosticResult
    {
        NotApplicable,
        Checked,
        DiagnosticAdded
    }

    private sealed record ProjectApplicationScope(
        IReadOnlyList<IStyle> Styles,
        IReadOnlyList<IDataTemplate> DataTemplates,
        bool HasDataContext,
        object? DataContext)
    {
        public static ProjectApplicationScope Empty { get; } = new([], [], false, null);
    }

    private sealed record PreviewSourceMetadata(
        double? DesignWidth,
        double? DesignHeight,
        string? DesignDataContextExpression,
        string? DesignDataContextObjectElement,
        IReadOnlyDictionary<string, string> Namespaces,
        IReadOnlyList<SourceBindingReference> BindingReferences,
        IReadOnlyList<SourceResourceReference> ResourceReferences)
    {
        public static PreviewSourceMetadata Empty { get; } = new(
            null,
            null,
            null,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal),
            [],
            []);
    }

    private sealed record PreviewDimensions(double Width, double Height);

    private sealed record PreviewPaths(
        string OutputPath,
        string? ProjectPath,
        string? ViewPath);

    private sealed record SourceBindingReference(
        string ElementPath,
        string ElementType,
        string TargetProperty,
        string Expression,
        string? BindingPath,
        string? ConverterResourceKey,
        bool IsCompiledBinding,
        string? DataTypeName,
        string? DataTypePath,
        IReadOnlyDictionary<string, string>? DataTypeNamespaces,
        bool HasExplicitSource,
        string? SourcePath);

    private sealed record SourceDataTypeReference(
        string TypeName,
        string ElementPath,
        IReadOnlyDictionary<string, string> Namespaces);

    private sealed record SourceResourceReference(
        string ElementPath,
        string ElementType,
        string TargetProperty,
        string Key,
        string Kind,
        string? SourcePath);

    private sealed record DesignDataResolution(bool HasValue, object? Value)
    {
        public static DesignDataResolution None { get; } = new(false, null);

        public static DesignDataResolution FromValue(object? value) => new(true, value);
    }

    private sealed record StaticMemberReference(string? Prefix, string TypeName, string MemberName);

    private sealed record XamlNamespaceReference(string ClrNamespace, string? AssemblyName);
}
