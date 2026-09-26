using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AvaScope.Protocol;

namespace AvaScope.Core;

/// <summary>Bounded source guidance, without evaluating MSBuild or running selected project code.</summary>
public static class BridgeIntegrationAdvisor
{
    private const int MaximumFileBytes = 1024 * 1024;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static CoreResult<BridgeIntegrationGuidanceResponse> Analyze(string projectPath, string? framework = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !Path.IsPathFullyQualified(projectPath) ||
                !string.Equals(Path.GetExtension(projectPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                return CoreResult<BridgeIntegrationGuidanceResponse>.Fail(new("integration_project_invalid", "Select one absolute .csproj path."));
            }

            projectPath = Path.GetFullPath(projectPath);
            var project = LoadProject(projectPath);
            var root = Path.GetDirectoryName(projectPath)!;
            var documents = new List<XDocument>();
            foreach (var name in new[] { "Directory.Build.props", "Directory.Packages.props" })
            {
                for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
                {
                    var candidate = Path.Combine(directory.FullName, name);
                    if (!File.Exists(candidate)) continue;
                    documents.Add(LoadProject(candidate));
                    break;
                }
            }

            documents.Add(project);
            var diagnostics = new List<string>
            {
                "Read-only static analysis; MSBuild imports, targets, custom SDKs, conditional compilation and user code are not evaluated. Review source hashes before applying guidance."
            };
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ambiguous = false;
            foreach (var element in documents.SelectMany(document => document.Descendants())
                         .Where(element => element.Parent?.Name.LocalName == "PropertyGroup"))
            {
                var name = element.Name.LocalName;
                if (name is not ("TargetFramework" or "TargetFrameworks" or "AvaloniaVersion" or "PublishAot" or "PublishTrimmed" or "EnableDefaultCompileItems")) continue;
                var condition = (string?)element.Attribute("Condition") ?? (string?)element.Parent?.Attribute("Condition");
                if (condition is not null && condition != $"'$({name})' == ''")
                {
                    ambiguous = true;
                    diagnostics.Add($"Conditional {name} requires review: {condition}");
                    continue;
                }

                if (condition is null || !properties.ContainsKey(name)) properties[name] = element.Value.Trim();
            }

            string Expand(string value)
            {
                for (var iteration = 0; iteration < 4 && value.Contains("$(", StringComparison.Ordinal); iteration++)
                    value = Regex.Replace(value, @"\$\(([A-Za-z0-9_]+)\)", match => properties.GetValueOrDefault(match.Groups[1].Value, match.Value), RegexOptions.None, RegexTimeout);
                return value;
            }

            var frameworks = Expand(properties.GetValueOrDefault("TargetFrameworks", properties.GetValueOrDefault("TargetFramework", "")))
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
            var selected = framework ?? (frameworks.Length == 1 ? frameworks[0] : null);
            if (selected is null || !frameworks.Contains(selected, StringComparer.Ordinal) ||
                !(selected == "net10.0" || selected.StartsWith("net10.0-windows", StringComparison.Ordinal)))
            {
                ambiguous = true;
                diagnostics.Add("Select one declared .NET 10 desktop target framework; unresolved, multiple or unsupported targets require review.");
            }

            var packageReferences = documents.SelectMany(document => document.Descendants()).Where(element => element.Name.LocalName == "PackageReference").ToArray();
            var versions = packageReferences.Where(element => (string?)element.Attribute("Include") is "Avalonia" or "Avalonia.Desktop" or "Avalonia.Headless")
                .Select(element => (string?)element.Attribute("Version") ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == "Version")?.Value ??
                    documents.SelectMany(document => document.Descendants()).LastOrDefault(candidate => candidate.Name.LocalName == "PackageVersion" &&
                        (string?)candidate.Attribute("Include") == (string?)element.Attribute("Include"))?.Attribute("Version")?.Value ?? "unresolved")
                .Select(Expand).Distinct(StringComparer.Ordinal).ToArray();
            var version = versions.Length == 1 ? versions[0] : null;
            if (!Version.TryParse(version, out var parsedVersion) || parsedVersion.Major != 12 || parsedVersion.Minor != 1)
            {
                ambiguous = true;
                diagnostics.Add("The standalone bootstrap requires Avalonia [12.1.0,12.2.0); resolve or align all engine package versions before integrating.");
            }

            if (properties.GetValueOrDefault("PublishAot") == "true" || properties.GetValueOrDefault("PublishTrimmed") == "true")
            {
                ambiguous = true;
                diagnostics.Add("Dynamic provider loading is unsupported in NativeAOT/trimmed output; select an untrimmed diagnostics configuration.");
            }

            if (properties.GetValueOrDefault("EnableDefaultCompileItems") == "false" ||
                project.Descendants().Any(element => element.Name.LocalName == "Compile" && (element.Attribute("Remove") is not null ||
                    ((string?)element.Attribute("Include"))?.Contains("..", StringComparison.Ordinal) == true)))
            {
                diagnostics.Add("Custom/linked Compile items are present; review effective source inclusion before applying snippets.");
            }

            var files = ReadSources(root);
            var existing = new List<IntegrationSourceLocation>();
            var startup = new List<IntegrationSourceLocation>();
            foreach (var (path, source) in files)
            {
                // Preserve offsets while excluding comments and strings from call-site matching.
                var code = Regex.Replace(source, "//[^\r\n]*|/\\*[\\s\\S]*?\\*/|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'",
                    match => new string(match.Value.Select(character => character is '\n' or '\r' ? character : ' ').ToArray()), RegexOptions.None, RegexTimeout);
                foreach (Match match in Regex.Matches(code, @"\b(?:AvaScopeBridge\s*\.\s*Activate|(?:AvaScope\s*\.\s*Bridge\s*\.\s*)?Bootstrap\s*\.\s*Start|OptionalProviderLoader\s*\.\s*TryStart(?:FromEnvironment)?|LoadOptionalProvider)\s*\(", RegexOptions.None, RegexTimeout))
                    if (!IsMethodDeclaration(code, match)) existing.Add(Location(path, source, match.Index));

                if (!Regex.IsMatch(code, @"\bclass\s+\w+\s*:\s*(?:Avalonia\.)?Application\b", RegexOptions.None, RegexTimeout) ||
                    !Regex.IsMatch(code, @"\boverride\s+void\s+OnFrameworkInitializationCompleted\s*\(\s*\)", RegexOptions.None, RegexTimeout)) continue;
                foreach (Match match in Regex.Matches(code, @"\bbase\s*\.\s*OnFrameworkInitializationCompleted\s*\(\s*\)\s*;", RegexOptions.None, RegexTimeout))
                    startup.Add(Location(path, source, match.Index));
            }

            var hasBridge = packageReferences.Any(element => (string?)element.Attribute("Include") == "AvaScope.Bridge") ||
                project.Descendants().Any(element => element.Name.LocalName == "ProjectReference" &&
                    ((string?)element.Attribute("Include"))?.Replace('\\', '/').EndsWith("/AvaScope.Bridge.csproj", StringComparison.OrdinalIgnoreCase) == true);
            if (hasBridge) diagnostics.Add("An AvaScope.Bridge reference already exists. Keep its existing declaration; do not add a duplicate.");
            if (packageReferences.Any(element => ((string?)element.Attribute("Include"))?.Contains("Diagnostics", StringComparison.OrdinalIgnoreCase) == true) ||
                files.Any(file => file.Source.Contains("AttachDevTools(", StringComparison.Ordinal)))
                diagnostics.Add("Existing diagnostics/DevTools integration detected. Preserve its startup and host-owned authorization.");

            var guidance = new List<IntegrationGuidance>();
            if (existing.Count > 0)
            {
                diagnostics.Add("Existing activation/loader call sites were found. No additive integration changes proposed; verify their compile-time guard and cleanup with verify-integration.");
            }
            else if (startup.Count != 1 || !files.Any(file => file.Source.Contains("IClassicDesktopStyleApplicationLifetime", StringComparison.Ordinal) || file.Source.Contains("ISingleViewApplicationLifetime", StringComparison.Ordinal)))
            {
                ambiguous = true;
                diagnostics.Add("No unique conventional Application.OnFrameworkInitializationCompleted/base call with a declared desktop or single-view lifetime was found. Locate the UI-thread startup after lifetime/content assignment manually; no insertion is guessed.");
            }
            else if (!ambiguous)
            {
                var sourceLocation = startup[0];
                var projectLocation = Location(projectPath, File.ReadAllText(projectPath), 0);
                const string flag = "<PropertyGroup Condition=\"'$(EnableUiInspection)' == 'true'\">\n  <DefineConstants>$(DefineConstants);ENABLE_UI_INSPECTION</DefineConstants>\n</PropertyGroup>";
                guidance.Add(new("package", projectLocation, "Choose only one integration mode. Add the opt-in property group inside Project. Preserve any existing bridge reference and ensure it is excluded from production configurations.",
                    flag + (hasBridge ? "" : $"\n<ItemGroup Condition=\"'$(EnableUiInspection)' == 'true'\">\n  <PackageReference Include=\"AvaScope.Bridge\" Version=\"{AvaScopeProduct.Version}\" />\n</ItemGroup>")));
                guidance.Add(new("package", sourceLocation, "Insert before this base call, after assigning MainWindow/MainView, on the UI thread. Retain the existing lifetime logic. Build diagnostics with -p:EnableUiInspection=true; normal builds leave it unset.",
                    "#if ENABLE_UI_INSPECTION\nglobal::AvaScope.Bridge.Bootstrap.Start();\n#endif"));
                guidance.Add(new("standalone", projectLocation, "Copy OptionalProviderLoader.cs from the verified provider distribution into this project root. Add these entries inside Project; the helper and all provider references are absent from ordinary builds. Remove an existing AvaScope reference only in a separately reviewed migration.",
                    flag + "\n<ItemGroup Condition=\"'$(EnableUiInspection)' != 'true'\">\n  <Compile Remove=\"OptionalProviderLoader.cs\" />\n</ItemGroup>"));
                guidance.Add(new("standalone", sourceLocation, "Insert before this base call, after assigning MainWindow/MainView, on the UI thread. Supply an explicit external UI_INSPECTION_PROVIDER_PATH only to the authorized diagnostics process. Optionally pin provider version and manifest SHA-256.",
                    "#if ENABLE_UI_INSPECTION\nvar inspection = global::OptionalDiagnostics.OptionalProviderLoader.TryStartFromEnvironment();\nif (!inspection.Success)\n    global::System.Console.Error.WriteLine($\"AvaScope: {inspection.Code}: {inspection.Message}\");\n#endif"));
            }

            return CoreResult<BridgeIntegrationGuidanceResponse>.Ok(new(projectPath,
                ambiguous ? "needs_review" : existing.Count > 0 ? "already_integrated" : "guidance_available", frameworks, selected, version, existing, guidance, diagnostics));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or ArgumentException or RegexMatchTimeoutException or InvalidDataException)
        {
            return CoreResult<BridgeIntegrationGuidanceResponse>.Fail(new("integration_analysis_failed", exception.Message));
        }
    }

    private static bool IsMethodDeclaration(string code, Match candidate)
    {
        // The name/parenthesis match also occurs in loader declarations. A body is
        // unambiguous; bodyless members additionally need a return-type header.
        var depth = 1;
        var index = candidate.Index + candidate.Length;
        for (; index < code.Length && depth > 0; index++)
        {
            if (code[index] == '(') depth++;
            else if (code[index] == ')') depth--;
        }

        if (depth != 0) return false;
        var following = code.AsSpan(index).TrimStart();
        if (following.StartsWith("{", StringComparison.Ordinal) || following.StartsWith("=>", StringComparison.Ordinal)) return true;
        if (!following.StartsWith(";", StringComparison.Ordinal)) return false;

        return Regex.IsMatch(code[..candidate.Index],
            @"(?:^|[;{}\]])(?:\s|\#[^\r\n]*)*(?:(?:public|private|protected|internal|static|abstract|virtual|override|sealed|extern|partial|unsafe|async|new|ref|readonly|delegate)\s+)*" +
            @"(?:\b(?!(?:return|await|throw|else|do|in)\b)[\w.:]+(?:\s*<[\w\s.,<>?\[\]]+>)?(?:\s*\[[,\s]*\])*\??|\([\w\s.,<>?\[\]]+\))\s+$",
            RegexOptions.None, RegexTimeout);
    }

    private static XDocument LoadProject(string path)
    {
        if (new FileInfo(path).Length > MaximumFileBytes) throw new InvalidDataException($"Project metadata exceeds 1 MiB: {path}");
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumFileBytes });
        return XDocument.Load(reader);
    }

    private static List<(string Path, string Source)> ReadSources(string root)
    {
        var files = new List<(string, string)>();
        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((root, 0));
        long totalBytes = 0;
        var directories = 0;
        while (pending.TryDequeue(out var directory))
        {
            if (++directories > 512 || directory.Depth > 12) throw new InvalidDataException("Source scan exceeds 512 directories or depth 12; select a smaller application project.");
            foreach (var entry in new DirectoryInfo(directory.Path).EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if (entry is DirectoryInfo)
                {
                    if (entry.Name is "bin" or "obj" or "artifacts" or "node_modules" || entry.Name.StartsWith('.')) continue;
                    pending.Enqueue((entry.FullName, directory.Depth + 1));
                }
                else if (entry is FileInfo file && file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    totalBytes += file.Length;
                    if (files.Count >= 512 || file.Length > MaximumFileBytes || totalBytes > 8 * MaximumFileBytes)
                        throw new InvalidDataException("Source scan exceeds 512 files, 1 MiB per file or 8 MiB total; select a smaller application project.");
                    files.Add((file.FullName, File.ReadAllText(file.FullName)));
                }
            }
        }

        return files;
    }

    private static IntegrationSourceLocation Location(string path, string source, int offset)
        => new(path, source.AsSpan(0, offset).Count('\n') + 1, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))));
}
