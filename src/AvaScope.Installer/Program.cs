using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;

namespace AvaScope.Installer;

internal static class Program
{
    private const string PayloadResourceName = "AvaScope.Installer.Payload.zip";
    private const string ManagedShimMarker = "AvaScope installer managed shim";
    private const string UninstallRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AvaScope";
    private static readonly string[] RequiredLegalFiles =
    [
        "LICENSE",
        "NOTICE",
        "LICENSE-SCOPE.md",
        "THIRD-PARTY-NOTICES.md"
    ];

    public static int Main(string[] args)
    {
        try
        {
            var options = InstallerOptions.Parse(args);
            if (options.ShowHelp)
            {
                WriteHelp();
                return 0;
            }

            if (options.Verify)
            {
                VerifyEmbeddedPayload();
                return 0;
            }

            if (options.UninstallWorker)
            {
                WaitForParent(options.ParentProcessId);
                Uninstall(options);
                ScheduleWorkerCleanup();
                return 0;
            }

            if (options.Uninstall)
            {
                if (TryLaunchUninstallWorker(options))
                {
                    return 0;
                }

                Uninstall(options);
                return 0;
            }

            Install(options);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"AvaScope installer failed: {exception.Message}");
            return 1;
        }
    }

    private static void Install(InstallerOptions options)
    {
        ValidateInstallRoot(options.InstallRoot);
        Directory.CreateDirectory(options.InstallRoot);

        var previousPathManaged = ReadPathManaged(options.InstallRoot);
        var stagingDirectory = Path.Combine(options.InstallRoot, $".installing-{Guid.NewGuid():N}");
        var previousDirectory = Path.Combine(options.InstallRoot, ".previous");
        var currentDirectory = Path.Combine(options.InstallRoot, "current");

        try
        {
            Directory.CreateDirectory(stagingDirectory);
            ExtractPayload(stagingDirectory);
            MakePayloadExecutable(stagingDirectory);
            ValidateInstalledPayload(stagingDirectory);

            var version = RunAndReadVersion(GetAvaScopeExecutable(stagingDirectory));
            DeleteInstallPath(previousDirectory, options.InstallRoot);

            if (Directory.Exists(currentDirectory))
            {
                Directory.Move(currentDirectory, previousDirectory);
            }

            try
            {
                Directory.Move(stagingDirectory, currentDirectory);
            }
            catch
            {
                if (Directory.Exists(previousDirectory) && !Directory.Exists(currentDirectory))
                {
                    Directory.Move(previousDirectory, currentDirectory);
                }

                throw;
            }

            DeleteInstallPath(previousDirectory, options.InstallRoot);

            Directory.CreateDirectory(options.BinDirectory);
            var commandPath = WriteCommandShim(options, currentDirectory);
            var uninstallerPath = CopyUninstaller(options);
            var pathManaged = previousPathManaged;
            if (!options.NoPathUpdate && OperatingSystem.IsWindows())
            {
                pathManaged = AddWindowsUserPath(options.BinDirectory) || previousPathManaged;
            }

            WriteDiscoveryManifest(
                options,
                currentDirectory,
                commandPath,
                uninstallerPath,
                version,
                pathManaged);

            if (OperatingSystem.IsWindows() && !options.NoRegistration && uninstallerPath is not null)
            {
                RegisterWindowsUninstaller(options, currentDirectory, uninstallerPath, version);
            }

            Console.WriteLine($"Installed AvaScope {version}.");
            Console.WriteLine($"Command: {commandPath}");
            Console.WriteLine($"Install root: {options.InstallRoot}");
            if (OperatingSystem.IsLinux() && !IsDirectoryOnPath(options.BinDirectory))
            {
                Console.WriteLine($"Add {options.BinDirectory} to PATH to run avascope from a new shell.");
            }
            else if (OperatingSystem.IsWindows() && !options.NoPathUpdate)
            {
                Console.WriteLine("Open a new terminal before using the updated user PATH.");
            }
        }
        finally
        {
            DeleteInstallPath(stagingDirectory, options.InstallRoot);
        }
    }

    private static void Uninstall(InstallerOptions options)
    {
        ValidateInstallRoot(options.InstallRoot);
        if (!Directory.Exists(options.InstallRoot))
        {
            Console.WriteLine("AvaScope is not installed at the selected install root.");
            return;
        }

        ValidateOwnedInstall(options.InstallRoot);
        var pathManaged = ReadPathManaged(options.InstallRoot);
        RemoveOwnedCommandShim(options);

        if (OperatingSystem.IsWindows())
        {
            if (pathManaged && !options.NoPathUpdate)
            {
                RemoveWindowsUserPath(options.BinDirectory);
            }

            if (!options.NoRegistration)
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryKey, throwOnMissingSubKey: false);
            }
        }

        if (Directory.Exists(options.InstallRoot))
        {
            Directory.Delete(options.InstallRoot, recursive: true);
        }

        Console.WriteLine("Uninstalled AvaScope.");
    }

    private static void ExtractPayload(string destinationDirectory)
    {
        using var payload = OpenPayload();
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        var destinationRoot = EnsureTrailingSeparator(Path.GetFullPath(destinationDirectory));

        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot, PathComparison))
            {
                throw new InvalidDataException($"Payload entry escapes the install directory: {entry.FullName}");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var source = entry.Open();
            using var destination = File.Create(destinationPath);
            source.CopyTo(destination);
        }
    }

    private static void ValidateInstalledPayload(string payloadDirectory)
    {
        var requiredFiles = new List<string>
        {
            GetAvaScopeExecutable(payloadDirectory),
            Path.Combine(payloadDirectory, "AvaScope.Mcp.dll"),
            Path.Combine(payloadDirectory, "AvaScope.PreviewHost.dll")
        };
        requiredFiles.AddRange(RequiredLegalFiles.Select(file => Path.Combine(payloadDirectory, file)));

        foreach (var path in requiredFiles)
        {
            if (!File.Exists(path))
            {
                throw new InvalidDataException($"Required install file is missing: {path}");
            }
        }
    }

    private static void VerifyEmbeddedPayload()
    {
        using var payload = OpenPayload();
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        var entries = archive.Entries
            .Select(static entry => entry.FullName.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var executableName = OperatingSystem.IsWindows() ? "avascope.exe" : "avascope";
        var required = new[]
        {
            executableName,
            "AvaScope.Mcp.dll",
            "AvaScope.PreviewHost.dll"
        }.Concat(RequiredLegalFiles);

        foreach (var name in required)
        {
            if (!entries.Contains(name))
            {
                throw new InvalidDataException($"Embedded payload is missing required file: {name}");
            }
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            product = "AvaScope",
            runtimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            payloadEntries = entries.Count,
            legalFiles = RequiredLegalFiles
        }));
    }

    private static Stream OpenPayload()
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName)
            ?? throw new InvalidOperationException(
                "This development build does not contain an installer payload. Use a packaged AvaScope installer artifact.");
    }

    private static string WriteCommandShim(InstallerOptions options, string currentDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            var commandPath = Path.Combine(options.BinDirectory, "avascope.cmd");
            File.WriteAllText(
                commandPath,
                $"@echo off\r\nrem {ManagedShimMarker}\r\n\"{Path.Combine(currentDirectory, "avascope.exe")}\" %*\r\nexit /b %ERRORLEVEL%\r\n");
            return commandPath;
        }

        var linuxCommandPath = Path.Combine(options.BinDirectory, "avascope");
        File.WriteAllText(
            linuxCommandPath,
            $"#!/usr/bin/env sh\n# {ManagedShimMarker}\nexec \"{Path.Combine(currentDirectory, "avascope")}\" \"$@\"\n");
        SetExecutable(linuxCommandPath);
        return linuxCommandPath;
    }

    private static string? CopyUninstaller(InstallerOptions options)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath) ||
            !File.Exists(processPath) ||
            string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var uninstallDirectory = Path.Combine(options.InstallRoot, "uninstall");
        Directory.CreateDirectory(uninstallDirectory);
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        var destination = Path.Combine(uninstallDirectory, $"avascope-uninstall{extension}");
        File.Copy(processPath, destination, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            SetExecutable(destination);
        }

        return destination;
    }

    private static void WriteDiscoveryManifest(
        InstallerOptions options,
        string currentDirectory,
        string commandPath,
        string? uninstallerPath,
        string version,
        bool pathManaged)
    {
        var manifest = new
        {
            schemaVersion = 1,
            product = "AvaScope",
            serviceName = "avascope",
            version,
            installMode = "per-user",
            installedAt = DateTimeOffset.UtcNow,
            installRoot = options.InstallRoot,
            installPath = currentDirectory,
            shimDirectory = options.BinDirectory,
            commandPath,
            executablePath = GetAvaScopeExecutable(currentDirectory),
            uninstallPath = uninstallerPath,
            pathEntryManaged = pathManaged,
            mcp = new
            {
                transport = "stdio",
                serverName = "avascope",
                commandPath,
                arguments = new[] { "mcp" },
                assemblyPath = Path.Combine(currentDirectory, "AvaScope.Mcp.dll")
            }
        };

        File.WriteAllText(
            Path.Combine(options.InstallRoot, "avascope.discovery.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static bool ReadPathManaged(string installRoot)
    {
        var manifestPath = Path.Combine(installRoot, "avascope.discovery.json");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return document.RootElement.TryGetProperty("pathEntryManaged", out var value) && value.GetBoolean();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ValidateOwnedInstall(string installRoot)
    {
        var manifestPath = Path.Combine(installRoot, "avascope.discovery.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException(
                $"Refusing to uninstall a directory without AvaScope discovery metadata: {installRoot}");
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            if (root.GetProperty("product").GetString() != "AvaScope" ||
                !PathEquals(root.GetProperty("installRoot").GetString() ?? string.Empty, installRoot))
            {
                throw new InvalidOperationException(
                    $"Refusing to uninstall a directory not owned by this AvaScope installation: {installRoot}");
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Refusing to uninstall because discovery metadata is invalid: {manifestPath}",
                exception);
        }
    }

    private static void RemoveOwnedCommandShim(InstallerOptions options)
    {
        var commandPath = OperatingSystem.IsWindows()
            ? Path.Combine(options.BinDirectory, "avascope.cmd")
            : Path.Combine(options.BinDirectory, "avascope");
        if (!File.Exists(commandPath))
        {
            return;
        }

        var contents = File.ReadAllText(commandPath);
        if (!contents.Contains(ManagedShimMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to remove an unmanaged command: {commandPath}");
        }

        File.Delete(commandPath);
        if (!PathEquals(options.BinDirectory, options.InstallRoot) &&
            Directory.Exists(options.BinDirectory) &&
            !Directory.EnumerateFileSystemEntries(options.BinDirectory).Any())
        {
            Directory.Delete(options.BinDirectory);
        }
    }

    private static string RunAndReadVersion(string executablePath)
    {
        var result = RunProcess(executablePath, "--version");
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            throw new InvalidDataException(
                $"Installed AvaScope payload did not report a version. {result.StandardError}".Trim());
        }

        return result.StandardOutput.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
    }

    private static ProcessResult RunProcess(string fileName, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Timed out running {fileName}.");
        }

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static void MakePayloadExecutable(string payloadDirectory)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        foreach (var name in new[] { "avascope", "AvaScope.Mcp", "AvaScope.PreviewHost" })
        {
            var path = Path.Combine(payloadDirectory, name);
            if (File.Exists(path))
            {
                SetExecutable(path);
            }
        }
    }

    private static void SetExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherExecute);
    }

    [SupportedOSPlatform("windows")]
    private static bool AddWindowsUserPath(string directory)
    {
        using var environmentKey = Registry.CurrentUser.CreateSubKey("Environment", writable: true)
            ?? throw new InvalidOperationException("Could not open the current-user environment registry key.");
        var currentPath = environmentKey.GetValue("Path", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames)
            as string ?? string.Empty;
        var entries = SplitWindowsPath(currentPath);
        if (entries.Any(entry => PathEntryEquals(entry, directory)))
        {
            return false;
        }

        entries.Add(directory);
        environmentKey.SetValue("Path", string.Join(';', entries), RegistryValueKind.ExpandString);
        return true;
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveWindowsUserPath(string directory)
    {
        using var environmentKey = Registry.CurrentUser.CreateSubKey("Environment", writable: true)
            ?? throw new InvalidOperationException("Could not open the current-user environment registry key.");
        var currentPath = environmentKey.GetValue("Path", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames)
            as string ?? string.Empty;
        var entries = SplitWindowsPath(currentPath)
            .Where(entry => !PathEntryEquals(entry, directory))
            .ToArray();
        environmentKey.SetValue("Path", string.Join(';', entries), RegistryValueKind.ExpandString);
    }

    private static List<string> SplitWindowsPath(string path)
    {
        return path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindowsUninstaller(
        InstallerOptions options,
        string currentDirectory,
        string uninstallerPath,
        string version)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryKey, writable: true)
            ?? throw new InvalidOperationException("Could not register the AvaScope uninstaller.");
        var uninstallCommand = $"\"{uninstallerPath}\" --uninstall";
        key.SetValue("DisplayName", "AvaScope");
        key.SetValue("DisplayVersion", version);
        key.SetValue("DisplayIcon", GetAvaScopeExecutable(currentDirectory));
        key.SetValue("Publisher", "AvaScope contributors");
        key.SetValue("URLInfoAbout", "https://github.com/RolandUI/AvaScope");
        key.SetValue("InstallLocation", options.InstallRoot);
        key.SetValue("UninstallString", uninstallCommand);
        key.SetValue("QuietUninstallString", uninstallCommand);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static bool TryLaunchUninstallWorker(InstallerOptions options)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath) ||
            !File.Exists(processPath) ||
            !IsUnderDirectory(processPath, options.InstallRoot))
        {
            return false;
        }

        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        var workerPath = Path.Combine(Path.GetTempPath(), $"avascope-uninstall-{Guid.NewGuid():N}{extension}");
        File.Copy(processPath, workerPath);
        if (!OperatingSystem.IsWindows())
        {
            SetExecutable(workerPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = workerPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--uninstall-worker");
        startInfo.ArgumentList.Add("--install-root");
        startInfo.ArgumentList.Add(options.InstallRoot);
        startInfo.ArgumentList.Add("--bin-dir");
        startInfo.ArgumentList.Add(options.BinDirectory);
        startInfo.ArgumentList.Add("--parent-pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        if (options.NoPathUpdate)
        {
            startInfo.ArgumentList.Add("--no-path-update");
        }
        if (options.NoRegistration)
        {
            startInfo.ArgumentList.Add("--no-registration");
        }

        Process.Start(startInfo);
        Console.WriteLine("AvaScope uninstall started.");
        return true;
    }

    private static void WaitForParent(int? parentProcessId)
    {
        if (parentProcessId is null)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(parentProcessId.Value);
            process.WaitForExit(30_000);
        }
        catch (ArgumentException)
        {
        }
    }

    private static void ScheduleWorkerCleanup()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            File.Delete(processPath);
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add($"timeout /t 1 /nobreak >nul & del /f /q \"{processPath}\"");
        Process.Start(startInfo);
    }

    private static void DeleteInstallPath(string path, string installRoot)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return;
        }

        if (!IsUnderDirectory(path, installRoot) || PathEquals(path, installRoot))
        {
            throw new InvalidOperationException($"Refusing to delete a path outside the install root: {path}");
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            File.Delete(path);
        }
    }

    private static void ValidateInstallRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root) ||
            PathEquals(fullPath, root) ||
            PathEquals(fullPath, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
        {
            throw new InvalidOperationException($"Unsafe install root: {fullPath}");
        }
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = EnsureTrailingSeparator(Path.GetFullPath(directory));
        return fullPath.StartsWith(fullDirectory, PathComparison);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            PathComparison);
    }

    private static bool PathEntryEquals(string entry, string directory)
    {
        try
        {
            return PathEquals(
                Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"')),
                directory);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsDirectoryOnPath(string directory)
    {
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(entry => PathEntryEquals(entry, directory));
    }

    private static string GetAvaScopeExecutable(string directory)
    {
        return Path.Combine(directory, OperatingSystem.IsWindows() ? "avascope.exe" : "avascope");
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void WriteHelp()
    {
        Console.WriteLine(
            """
            AvaScope per-user installer

              avascope-installer [--install-root <path>] [--bin-dir <path>]
                                 [--no-path-update] [--no-registration]
              avascope-installer --uninstall [same options]
              avascope-installer --verify

            The default install is non-admin. Windows uses %LOCALAPPDATA%\AvaScope.
            Linux uses $XDG_DATA_HOME/avascope or ~/.local/share/avascope and ~/.local/bin.
            """);
    }

    private sealed record InstallerOptions(
        bool Uninstall,
        bool Verify,
        bool ShowHelp,
        bool NoPathUpdate,
        bool NoRegistration,
        bool UninstallWorker,
        int? ParentProcessId,
        string InstallRoot,
        string BinDirectory)
    {
        public static InstallerOptions Parse(string[] args)
        {
            var uninstall = false;
            var verify = false;
            var showHelp = false;
            var noPathUpdate = false;
            var noRegistration = false;
            var uninstallWorker = false;
            int? parentProcessId = null;
            string? installRoot = null;
            string? binDirectory = null;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--uninstall":
                        uninstall = true;
                        break;
                    case "--verify":
                        verify = true;
                        break;
                    case "--help":
                    case "-h":
                        showHelp = true;
                        break;
                    case "--no-path-update":
                        noPathUpdate = true;
                        break;
                    case "--no-registration":
                        noRegistration = true;
                        break;
                    case "--uninstall-worker":
                        uninstallWorker = true;
                        break;
                    case "--install-root":
                        installRoot = ReadValue(args, ref index, "--install-root");
                        break;
                    case "--bin-dir":
                        binDirectory = ReadValue(args, ref index, "--bin-dir");
                        break;
                    case "--parent-pid":
                        var value = ReadValue(args, ref index, "--parent-pid");
                        if (!int.TryParse(value, out var parsedParentProcessId) || parsedParentProcessId <= 0)
                        {
                            throw new ArgumentException("--parent-pid must be a positive integer.");
                        }

                        parentProcessId = parsedParentProcessId;
                        break;
                    default:
                        throw new ArgumentException($"Unknown installer argument: {args[index]}");
                }
            }

            installRoot = Path.GetFullPath(installRoot ?? GetDefaultInstallRoot());
            binDirectory = Path.GetFullPath(binDirectory ?? GetDefaultBinDirectory(installRoot));
            return new InstallerOptions(
                uninstall,
                verify,
                showHelp,
                noPathUpdate,
                noRegistration,
                uninstallWorker,
                parentProcessId,
                installRoot,
                binDirectory);
        }

        private static string ReadValue(string[] args, ref int index, string argument)
        {
            if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
            {
                throw new ArgumentException($"{argument} requires a value.");
            }

            return args[index];
        }

        private static string GetDefaultInstallRoot()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AvaScope");
            }

            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return !string.IsNullOrWhiteSpace(xdgDataHome)
                ? Path.Combine(xdgDataHome, "avascope")
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local",
                    "share",
                    "avascope");
        }

        private static string GetDefaultBinDirectory(string installRoot)
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(installRoot, "bin");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "bin");
        }
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
