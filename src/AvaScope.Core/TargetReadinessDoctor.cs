using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class TargetReadinessDoctor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CoreResult<TargetReadinessResponse>> CheckAsync(TargetReadinessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TimeoutMs is < 100 or > 30000 || request.Backend is not ("auto" or "headless" or "x11" or "wayland" or "win32" or "macos") ||
            (request.ProfileFile is null) != (request.ProfileName is null) ||
            new[] { request.ProjectPath, request.AssemblyPath, request.ProfileFile, request.ProviderDirectory, request.ExpectedInstallationRoot }
                .Any(path => path is not null && !Path.IsPathFullyQualified(path)))
            return CoreResult<TargetReadinessResponse>.Fail(new("target_readiness_invalid", "Use absolute selected paths, both profileFile/profileName, a known backend and timeoutMs 100–30000."));
        var checks = new List<DoctorCheck>();
        var probePath = new PreviewHostClient().HostAssemblyPath;
        var origins = new[]
        {
            DiagnosticOriginBuilder.Create("core", typeof(TargetReadinessDoctor).Assembly.Location),
            DiagnosticOriginBuilder.Create("protocol", typeof(AvaScopeProduct).Assembly.Location),
            DiagnosticOriginBuilder.Create("previewHost", probePath)
        };
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        var project = request.ProjectPath;
        var selectedFramework = request.Framework;
        var assembly = request.AssemblyPath;
        var providerDirectory = request.ProviderDirectory;
        var providerVersion = request.ExpectedProviderVersion;
        var providerHash = request.ExpectedManifestSha256;
        var backend = request.Backend;
        void Check(string name, bool passed, string code, string message, string remediation, string stage)
            => checks.Add(new(name, passed ? "available" : "unavailable", message,
                error: passed ? null : new(code, message), stage: stage, remediation: passed ? null : remediation));

        try
        {
            if (request.ProfileFile is not null)
            {
                var profile = AgentTestProfiles.ResolveRequest(request.ProfileFile, request.ProfileName!);
                Check("test_profile", profile.Success, profile.Error?.Code ?? "target_profile_invalid", profile.Error?.Message ?? "The selected named profile resolved without execution.", "Resolve the profile's missing references, incompatible provider or invalid fields.", "configuration");
                if (!profile.Success) return Complete();
                var scenario = profile.Value!;
                project ??= scenario.Build?.ProjectPath ?? scenario.Launch?.ProjectPath;
                selectedFramework ??= scenario.Build?.Framework ?? scenario.Launch?.Framework;
                if (scenario.Launch is { } launch)
                {
                    foreach (var pair in launch.Environment) environment[pair.Key] = pair.Value;
                    if (assembly is null && launch.Command == "dotnet" && launch.ArgumentList.FirstOrDefault() is { } candidate && candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        assembly = Path.GetFullPath(candidate, launch.WorkingDirectory ?? Path.GetDirectoryName(request.ProfileFile)!);
                }
                providerDirectory ??= environment.GetValueOrDefault("UI_INSPECTION_PROVIDER_PATH");
                providerVersion ??= environment.GetValueOrDefault("UI_INSPECTION_PROVIDER_VERSION");
                providerHash ??= environment.GetValueOrDefault("UI_INSPECTION_PROVIDER_SHA256");
            }
            if (backend == "auto") backend = OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "macos" :
                !string.IsNullOrWhiteSpace(environment.GetValueOrDefault("WAYLAND_DISPLAY") ?? Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ? "wayland" : "x11";
            Check("dotnet_runtime", Environment.Version.Major == 10, "target_runtime_incompatible", $"Doctor/probe runtime: .NET {Environment.Version.Major}.", "Use the supported .NET 10 tool/runtime distribution.", "compatibility");
            var roots = origins.Where(origin => origin.Exists).Select(origin => origin.RootDirectory).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).ToArray();
            var expectedRoot = request.ExpectedInstallationRoot is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.ExpectedInstallationRoot));
            Check("installation_origins", roots.Length == 1 && (expectedRoot is null || string.Equals(expectedRoot, roots[0], OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)),
                "target_installation_root_conflict", "Active Core, Protocol and probe artifact roots were compared.", "Use CLI/MCP/probe files from the same installation root and verify the selected tool path.", "origins");
            if (project is not null)
            {
                var guidance = BridgeIntegrationAdvisor.Analyze(project, selectedFramework);
                if (!guidance.Success) Check("target_project", false, "target_project_invalid", guidance.Error!.Message, "Select a readable Avalonia application project.", "compatibility");
                else
                {
                    var tfm = guidance.Value!.SelectedFramework;
                    Check("target_framework", tfm == "net10.0" || tfm?.StartsWith("net10.0-windows", StringComparison.Ordinal) == true,
                        "target_runtime_incompatible", "The target framework was read from project metadata.", "Select an untrimmed .NET 10 desktop target in the profile.", "compatibility");
                    Check("avalonia_version", Version.TryParse(guidance.Value.AvaloniaVersion, out var version) && version.Major == 12 && version.Minor == 1,
                        "target_avalonia_incompatible", $"Declared Avalonia version: {guidance.Value.AvaloniaVersion ?? "unresolved"}.", "Align Avalonia engine packages to the provider's supported 12.1.x range.", "compatibility");
                    if (guidance.Value.Diagnostics.Any(message => message.Contains("NativeAOT", StringComparison.Ordinal)))
                        Check("dynamic_loading", false, "target_dynamic_loading_unsupported", "The selected project declares AOT/trimmed output.", "Select an untrimmed diagnostics configuration.", "compatibility");
                }
            }
            if (assembly is not null) CheckAssembly(assembly, checks);
            if (providerDirectory is not null)
            {
                var provider = ProviderVerifier.Verify(providerDirectory, providerVersion, providerHash);
                Check("provider", provider.Success, provider.Error?.Code ?? "target_provider_incompatible", provider.Error?.Message ?? "Selected provider inventory, exact pins and declared compatibility passed.", "Supply a complete provider distribution compatible with the target runtime/Avalonia version.", "provider");
            }
            var probe = await RunProbeAsync(probePath, new(backend, request.NativeInput, request.NativeScreenshot, request.NativeDialogs), environment, request.TimeoutMs, cancellationToken);
            checks.AddRange(probe);
            return Complete();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException or BadImageFormatException or OperationCanceledException or InvalidOperationException or KeyNotFoundException)
        {
            // Do not echo launch environment or arbitrary native error output into tool diagnostics.
            checks.Add(new("target_readiness", "unavailable", exception is OperationCanceledException ? "Readiness check was cancelled." : "Selected target metadata could not be inspected.",
                error: new("target_metadata_invalid", "Check selected file accessibility and metadata format."), stage: "metadata", remediation: "Use readable, complete target build output and a valid project/profile."));
            return Complete();
        }

        CoreResult<TargetReadinessResponse> Complete() => CoreResult<TargetReadinessResponse>.Ok(new(
            checks.Any(check => check.Status is "unavailable" or "unsupported" or "incompatible") ? "unavailable" : "available", backend, checks.Take(64).ToArray(), origins));
    }

    private static void CheckAssembly(string path, List<DoctorCheck> checks)
    {
        if (!File.Exists(path))
        {
            checks.Add(new("target_assembly", "unavailable", "The selected target assembly does not exist.", path,
                new("target_assembly_missing", "Build the selected diagnostics configuration before launch."), "dependencies", "Build the selected project or correct the explicit launch assembly path."));
            return;
        }
        var identity = AssemblyName.GetAssemblyName(path);
        checks.Add(new("target_assembly", "available", $"Managed assembly metadata read: {identity.Name}.", path, stage: "dependencies"));
        var runtimePath = Path.ChangeExtension(path, ".runtimeconfig.json");
        if (File.Exists(runtimePath))
        {
            using var runtime = ReadJson(runtimePath);
            var options = runtime.RootElement.GetProperty("runtimeOptions");
            var tfm = options.TryGetProperty("tfm", out var framework) ? framework.GetString() : null;
            checks.Add(new("target_runtime_config", tfm == "net10.0" ? "available" : "unavailable", "Target runtime configuration was inspected.", runtimePath,
                tfm == "net10.0" ? null : new("target_runtime_incompatible", "The built host must target .NET 10."), "compatibility", tfm == "net10.0" ? null : "Rebuild the compatible diagnostics target."));
        }
        else checks.Add(new("target_runtime_config", "not_checked", "No runtimeconfig.json is present; this may be a library or incomplete executable output.", stage: "compatibility"));
        var dependenciesPath = Path.ChangeExtension(path, ".deps.json");
        if (!File.Exists(dependenciesPath))
        {
            checks.Add(new("target_dependencies", "unavailable", "The selected output has no dependency manifest.", error: new("target_dependencies_missing", "Supply the complete framework-dependent host output."), stage: "dependencies"));
            return;
        }
        using var dependencies = ReadJson(dependenciesPath);
        if (dependencies.RootElement.TryGetProperty("libraries", out var libraries))
        {
            var avalonia = libraries.EnumerateObject().Select(item => item.Name).FirstOrDefault(name => name.StartsWith("Avalonia/", StringComparison.Ordinal));
            var valid = avalonia is not null && Version.TryParse(avalonia["Avalonia/".Length..], out var version) && version.Major == 12 && version.Minor == 1;
            checks.Add(new("built_avalonia_version", valid ? "available" : "unavailable", "Built Avalonia engine version was checked from deps.json.",
                error: valid ? null : new("target_avalonia_incompatible", "The built host is not a resolved Avalonia 12.1.x application."), stage: "compatibility"));
        }
        var missing = new List<string>();
        var directory = Path.GetDirectoryName(path)!;
        var count = 0;
        foreach (var target in dependencies.RootElement.GetProperty("targets").EnumerateObject())
        foreach (var library in target.Value.EnumerateObject())
        {
            if (!library.Value.TryGetProperty("runtime", out var assets)) continue;
            foreach (var asset in assets.EnumerateObject())
            {
                if (++count > 2048) throw new JsonException("Dependency manifest exceeds 2048 managed assets.");
                var name = Path.GetFileName(asset.Name);
                if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !File.Exists(Path.Combine(directory, name))) missing.Add(name);
            }
        }
        checks.Add(new("target_dependencies", missing.Count == 0 ? "available" : "unavailable", missing.Count == 0 ? "Declared private managed dependencies are present in the selected output." : $"Missing managed dependencies: {string.Join(", ", missing.Take(12))}.",
            error: missing.Count == 0 ? null : new("target_dependency_missing", "A declared host dependency is absent."), stage: "dependencies", remediation: missing.Count == 0 ? null : "Publish/copy the full host output, preserving dependencies."));
    }

    private static JsonDocument ReadJson(string path)
    {
        if (new FileInfo(path).Length > 1024 * 1024) throw new JsonException("Target metadata exceeds 1 MiB.");
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }

    private static async Task<IReadOnlyList<DoctorCheck>> RunProbeAsync(string path, PlatformReadinessProbeRequest request,
        IReadOnlyDictionary<string, string> environment, int timeoutMs, CancellationToken cancellationToken)
    {
        DoctorCheck Failed(string code, string message) => new("platform_probe", "unavailable", message, error: new(code, message),
            stage: "platform", remediation: "Verify the selected AvaScope probe installation and platform dependencies.");
        if (!File.Exists(path)) return [Failed("target_probe_missing", "The installed isolated platform probe is unavailable.")];
        using var process = new Process { StartInfo = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        process.StartInfo.ArgumentList.Add(path);
        process.StartInfo.ArgumentList.Add("--readiness-probe");
        process.StartInfo.ArgumentList.Add(JsonSerializer.Serialize(request, JsonOptions));
        foreach (var name in new[] { "DISPLAY", "XAUTHORITY", "WAYLAND_DISPLAY", "XDG_RUNTIME_DIR", "DBUS_SESSION_BUS_ADDRESS", "LANG", "LC_ALL" })
            if (environment.TryGetValue(name, out var value)) process.StartInfo.Environment[name] = value;
        try { if (!process.Start()) return [Failed("target_probe_start_failed", "The platform probe could not start.")]; }
        catch (System.ComponentModel.Win32Exception) { return [Failed("target_probe_start_failed", "The .NET platform probe could not start.")]; }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutMs);
        var stdout = ReadBoundedAsync(process.StandardOutput, timeout.Token);
        var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var json = await stdout;
            await stderr;
            if (process.ExitCode != 0) return [Failed("target_probe_failed", "The isolated native probe exited unsuccessfully; no application code or bridge was loaded.")];
            if (JsonSerializer.Deserialize<DoctorCheck[]>(json, JsonOptions) is not { Length: <= 32 } checks)
                return [Failed("target_probe_invalid", "The probe returned an invalid bounded result.")];
            return [.. checks, new("probe_cleanup", "available", "The owned platform probe exited and all handles were released.", stage: "cleanup",
                evidence: new Dictionary<string, string> { ["processId"] = process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), ["exited"] = "true" })];
        }
        catch (OperationCanceledException) { return [Failed("target_probe_timed_out", "The isolated native probe reached its deadline or was cancelled.")]; }
        catch (Exception exception) when (exception is JsonException or IOException) { return [Failed("target_probe_invalid", "The native probe returned an invalid or oversized result.")]; }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cleanupTimeout.Token);
            try { await Task.WhenAll(stdout, stderr).WaitAsync(cleanupTimeout.Token); }
            catch (Exception exception) when (exception is OperationCanceledException or IOException) { }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[32769];
        var length = 0;
        while (length < buffer.Length)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(length), cancellationToken);
            if (count == 0) return new string(buffer, 0, length);
            length += count;
        }
        throw new IOException("Native probe output exceeds 32 KiB.");
    }
}
