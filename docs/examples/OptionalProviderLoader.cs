using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

namespace OptionalDiagnostics;

// Copy this file into the host, or link it as source. It uses only the .NET BCL.
public static class OptionalProviderLoader
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly ConcurrentDictionary<string, LoadedProvider> Loaded = new(PathComparer);
    private static readonly object SyncRoot = new();

    public static ProviderLoadResult TryStartFromEnvironment(
        string environmentVariable = "UI_INSPECTION_PROVIDER_PATH",
        string? expectedVersion = null,
        string? expectedManifestSha256 = null)
    {
        var directory = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(directory)
            ? new(true, false, "AVASCOPE_PROVIDER_NOT_CONFIGURED", $"{environmentVariable} is unset; inspection is disabled.")
            : TryStart(directory, expectedVersion, expectedManifestSha256);
    }

    public static ProviderLoadResult Verify(
        string directory, string? expectedVersion = null, string? expectedManifestSha256 = null)
    {
        try
        {
            var provider = ReadProvider(directory, expectedVersion, expectedManifestSha256, verifyHost: false);
            return new(true, false, "AVASCOPE_PROVIDER_VERIFIED", "Provider integrity, runtime and platform verified; host Avalonia compatibility is checked at activation.",
                provider.Directory, provider.Manifest.ProviderVersion, provider.ManifestHash);
        }
        catch (Exception exception) when (IsProviderFailure(exception))
        {
            return Failure(exception);
        }
    }

    public static ProviderLoadResult TryStart(
        string directory, string? expectedVersion = null, string? expectedManifestSha256 = null)
    {
        try
        {
            lock (SyncRoot)
            {
                var provider = ReadProvider(directory, expectedVersion, expectedManifestSha256, verifyHost: true);
                if (Loaded.TryGetValue(provider.Directory, out var loaded))
                {
                    if (!string.Equals(loaded.ManifestHash, provider.ManifestHash, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "AVASCOPE_PROVIDER_CHANGED: A different provider was already loaded from this path. Restart the host before replacing provider files.");
                    }
                }
                else
                {
                    if (!Loaded.IsEmpty || AssemblyLoadContext.Default.Assemblies.Any(assembly => assembly.GetName().Name == "AvaScope.Bridge"))
                    {
                        throw new InvalidOperationException(
                            "AVASCOPE_PROVIDER_ALREADY_LOADED: This host already loaded a bridge. Reuse its bootstrap or restart the host before selecting another provider.");
                    }

                    var context = new ProviderLoadContext(provider);
                    var assembly = context.LoadFromAssemblyPath(Path.Combine(provider.Directory, "AvaScope.Bridge.dll"));
                    var bootstrap = assembly.GetType("AvaScope.Bridge.Bootstrap", throwOnError: true)!;
                    var start = bootstrap.GetMethod("Start", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
                    if (start is null || start.ReturnType != typeof(string))
                    {
                        throw new MissingMethodException(
                            "AVASCOPE_BOOTSTRAP_MISSING: Expected public static string AvaScope.Bridge.Bootstrap.Start().");
                    }

                    loaded = new(provider.ManifestHash, start);
                    Loaded.AddOrUpdate(provider.Directory, loaded, (_, _) => loaded);
                }

                var sessionId = loaded.Start.Invoke(null, null) as string;
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    throw new InvalidOperationException("AVASCOPE_BOOTSTRAP_INVALID: Start() did not return a session id.");
                }

                return new(true, true, "AVASCOPE_PROVIDER_STARTED", "The explicit provider bootstrap activated the local bridge.",
                    provider.Directory, provider.Manifest.ProviderVersion, provider.ManifestHash, sessionId);
            }
        }
        catch (Exception exception) when (IsProviderFailure(exception))
        {
            return Failure(exception);
        }
    }

    private static VerifiedProvider ReadProvider(string directory, string? expectedVersion, string? expectedHash, bool verifyHost)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported || Environment.Version.Major != 10)
        {
            throw new NotSupportedException("AVASCOPE_RUNTIME_INCOMPATIBLE: The external provider requires untrimmed .NET 10; NativeAOT is unsupported.");
        }

        if (!Path.IsPathFullyQualified(directory))
        {
            throw new ArgumentException("AVASCOPE_PROVIDER_PATH_INVALID: Supply an absolute external provider directory.");
        }

        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var manifestPath = Path.Combine(directory, "provider-manifest.json");
        if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 1024 * 1024)
        {
            throw new FileNotFoundException("AVASCOPE_PROVIDER_MANIFEST_INVALID: A provider-manifest.json of at most 1 MiB is required.", manifestPath);
        }

        var bytes = File.ReadAllBytes(manifestPath);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (expectedHash is not null && !string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("AVASCOPE_PROVIDER_PIN_MISMATCH: The manifest SHA-256 differs from the requested artifact identity.");
        }

        var manifest = JsonSerializer.Deserialize<ProviderManifest>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("AVASCOPE_PROVIDER_MANIFEST_INVALID: Empty provider manifest.");
        if (manifest.SchemaVersion != 1 || !Version.TryParse(manifest.ProviderVersion, out _)
            || manifest.DotnetMajor != 10 || manifest.BootstrapAssembly != "AvaScope.Bridge.dll"
            || manifest.BootstrapType != "AvaScope.Bridge.Bootstrap" || manifest.BootstrapMethod != "Start"
            || manifest.Files is not { Length: > 0 and <= 512 }
            || manifest.HostSharedAssemblies is not { Length: > 0 and <= 64 }
            || manifest.HostSharedAssemblyIdentities is null
            || manifest.HostSharedAssemblyIdentities.Count != manifest.HostSharedAssemblies.Length)
        {
            throw new InvalidDataException("AVASCOPE_PROVIDER_MANIFEST_INVALID: Unsupported schema, entry point, runtime or dependency inventory.");
        }

        if (expectedVersion is not null && !string.Equals(manifest.ProviderVersion, expectedVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"AVASCOPE_PROVIDER_PIN_MISMATCH: Requested provider {expectedVersion}, found {manifest.ProviderVersion}.");
        }

        var rid = (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsMacOS() ? "osx" : "unsupported")
            + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        if (manifest.RuntimeIdentifiers?.Contains(rid, StringComparer.Ordinal) != true)
        {
            throw new NotSupportedException($"AVASCOPE_PLATFORM_INCOMPATIBLE: Provider does not support {rid}.");
        }

        if (!Version.TryParse(manifest.AvaloniaMinimumVersion, out var minimum)
            || !Version.TryParse(manifest.AvaloniaMaximumExclusiveVersion, out var maximum)
            || minimum != new Version(12, 1, 0) || maximum != new Version(12, 2, 0)
            || manifest.HostSharedAssemblies.Any(name => string.IsNullOrWhiteSpace(name) || !name.StartsWith("Avalonia", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("AVASCOPE_PROVIDER_MANIFEST_INVALID: Invalid host-shared Avalonia compatibility contract.");
        }

        foreach (var name in manifest.HostSharedAssemblies)
        {
            if (!manifest.HostSharedAssemblyIdentities.TryGetValue(name, out var referenceIdentity))
            {
                throw new InvalidDataException($"AVASCOPE_PROVIDER_MANIFEST_INVALID: Missing host-shared identity for {name}.");
            }

            var reference = new AssemblyName(referenceIdentity);
            if (reference.Name != name || reference.Version is null)
            {
                throw new InvalidDataException("AVASCOPE_PROVIDER_MANIFEST_INVALID: Invalid host-shared assembly identity.");
            }

            if (!verifyHost)
            {
                continue;
            }

            // Resolve only through the host. Never satisfy Avalonia from provider-private files.
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name));
            var actual = assembly.GetName();
            var versionText = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            // Auxiliary Avalonia assemblies (for example DesignerSupport) have independent versions.
            // Check their referenced identity; the Avalonia product range applies to the two engine assemblies.
            if (actual.Version is null || actual.Version < reference.Version
                || actual.Version.Major != reference.Version.Major || actual.Version.Minor != reference.Version.Minor
                || !(actual.GetPublicKeyToken() ?? []).SequenceEqual(reference.GetPublicKeyToken() ?? [])
                || (name is "Avalonia.Base" or "Avalonia.Controls"
                    && (!Version.TryParse(versionText?.Split('+', '-')[0], out var version) || version < minimum || version >= maximum)))
            {
                throw new NotSupportedException($"AVASCOPE_AVALONIA_INCOMPATIBLE: Host {name} reports '{versionText ?? "unknown"}'; requires [{minimum}, {maximum}).");
            }
        }

        var verifiedFiles = new HashSet<string>(PathComparer);
        foreach (var file in manifest.Files)
        {
            if (file is null || string.IsNullOrWhiteSpace(file.Path) || Path.IsPathRooted(file.Path)
                || file.Path.Contains('\\') || file.Path.Contains(':') || file.Path.Split('/').Any(part => part is "" or "." or "..")
                || file.Length < 0 || file.Sha256 is null || file.Sha256.Length != 64)
            {
                throw new InvalidDataException("AVASCOPE_PROVIDER_MANIFEST_INVALID: Invalid dependency path, size or checksum.");
            }

            var path = Path.GetFullPath(Path.Combine(directory, file.Path));
            if (!verifiedFiles.Add(path))
            {
                throw new InvalidDataException("AVASCOPE_PROVIDER_MANIFEST_INVALID: Duplicate dependency path.");
            }

            for (var current = path; !PathComparer.Equals(current, directory); current = Path.GetDirectoryName(current)!)
            {
                if (current is null || (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("AVASCOPE_PROVIDER_PATH_INVALID: Dependencies must stay inside the provider directory without symbolic links.");
                }
            }

            using var stream = File.OpenRead(path);
            if (stream.Length != file.Length || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(stream)), file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"AVASCOPE_PROVIDER_CORRUPT: Dependency checksum mismatch: {file.Path}.");
            }
        }

        foreach (var required in new[] { "AvaScope.Bridge.dll", "AvaScope.Bridge.deps.json", "AvaScope.Core.dll", "AvaScope.Protocol.dll" })
        {
            if (!verifiedFiles.Contains(Path.Combine(directory, required)))
            {
                throw new InvalidDataException($"AVASCOPE_PROVIDER_INCOMPLETE: Required dependency is not verified: {required}.");
            }
        }

        var providerVersion = Version.Parse(manifest.ProviderVersion);
        foreach (var name in new[] { "AvaScope.Bridge", "AvaScope.Core", "AvaScope.Protocol" })
        {
            var identity = AssemblyName.GetAssemblyName(Path.Combine(directory, name + ".dll"));
            if (identity.Name != name || identity.Version is not { } assemblyVersion
                || assemblyVersion.Major != providerVersion.Major || assemblyVersion.Minor != providerVersion.Minor
                || assemblyVersion.Build != providerVersion.Build)
            {
                throw new BadImageFormatException($"AVASCOPE_PROVIDER_ASSEMBLY_INVALID: {name} does not match provider {manifest.ProviderVersion}.");
            }
        }

        return new(directory, manifest, hash, verifiedFiles);
    }

    private static bool IsProviderFailure(Exception exception) => exception is IOException or InvalidDataException or UnauthorizedAccessException
        or ArgumentException or NotSupportedException or InvalidOperationException or JsonException or BadImageFormatException
        or TypeLoadException or MissingMemberException or TargetInvocationException;

    private static ProviderLoadResult Failure(Exception exception)
    {
        var cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
        var message = cause.Message;
        var separator = message.IndexOf(':');
        var code = message.StartsWith("AVASCOPE_", StringComparison.Ordinal) && separator > 0
            ? message[..separator] : cause switch
            {
                BadImageFormatException => "AVASCOPE_PROVIDER_ASSEMBLY_INVALID",
                FileNotFoundException or DirectoryNotFoundException => "AVASCOPE_PROVIDER_DEPENDENCY_MISSING",
                MissingMemberException or TypeLoadException => "AVASCOPE_BOOTSTRAP_INCOMPATIBLE",
                _ => "AVASCOPE_PROVIDER_LOAD_FAILED"
            };
        return new(false, false, code, message);
    }

    private sealed class ProviderLoadContext(VerifiedProvider provider) : AssemblyLoadContext(isCollectible: false)
    {
        private readonly AssemblyDependencyResolver _resolver = new(Path.Combine(provider.Directory, "AvaScope.Bridge.dll"));

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true)
            {
                if (!provider.Manifest.HostSharedAssemblies.Contains(assemblyName.Name, StringComparer.Ordinal))
                {
                    throw new FileLoadException($"AVASCOPE_PROVIDER_INCOMPLETE: Undeclared host-shared dependency {assemblyName.Name}.");
                }

                return Default.LoadFromAssemblyName(assemblyName);
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(RequireVerified(path));
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? 0 : LoadUnmanagedDllFromPath(RequireVerified(path));
        }

        private string RequireVerified(string path) => provider.Files.Contains(Path.GetFullPath(path))
            ? path : throw new FileLoadException("AVASCOPE_PROVIDER_DEPENDENCY_UNVERIFIED: Resolution escaped the verified provider inventory.");
    }

    private sealed record LoadedProvider(string ManifestHash, MethodInfo Start);
    private sealed record VerifiedProvider(string Directory, ProviderManifest Manifest, string ManifestHash, HashSet<string> Files);
    private sealed record ProviderManifest(int SchemaVersion, string ProviderVersion, int DotnetMajor,
        string AvaloniaMinimumVersion, string AvaloniaMaximumExclusiveVersion, string[]? RuntimeIdentifiers,
        string BootstrapAssembly, string BootstrapType, string BootstrapMethod, string[] HostSharedAssemblies,
        IReadOnlyDictionary<string, string> HostSharedAssemblyIdentities, ProviderFile[] Files);
    private sealed record ProviderFile(string Path, long Length, string Sha256);
}

public sealed record ProviderLoadResult(bool Success, bool Activated, string Code, string Message,
    string? Directory = null, string? ProviderVersion = null, string? ManifestSha256 = null, string? SessionId = null);
