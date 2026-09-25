using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Installer;

public sealed class WindowsInstallerFactAttribute : FactAttribute
{
    public WindowsInstallerFactAttribute()
    { if (!OperatingSystem.IsWindows()) Skip = "Controlled Windows installer process fixture."; }
}

public sealed class WindowsInstallerTheoryAttribute : TheoryAttribute
{
    public WindowsInstallerTheoryAttribute()
    { if (!OperatingSystem.IsWindows()) Skip = "Controlled Windows installer process fixture."; }
}

public sealed class InstallerWorkflowTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [WindowsInstallerFact]
    public async Task InstallerProcessTimeoutRetainsPartialOutputAndStopsItsOwnedChild()
    {
        Process? owned = null;
        try
        {
            var failure = await Record.ExceptionAsync(() => RunProcessAsync("powershell",
                ["-NoProfile", "-Command", "[Console]::Out.WriteLine(('x' * 12000)); [Console]::Out.WriteLine('installer-fixture-ready'); [Console]::Error.WriteLine('installer-fixture-stderr'); Start-Sleep -Seconds 120"],
                FindRepositoryRoot(), process =>
                {
                    owned = Process.GetProcessById(process.Id);
                    _ = owned.SafeHandle; // Retain the exact child identity even after the helper disposes its handle.
                }));
            Assert.NotNull(failure);
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.NotNull(owned);
            Assert.True(owned.HasExited, "The timed-out installer test child is still running after RunProcessAsync returned.");
            Assert.Equal("process_wait", failure.Data["processPhase"]);
            Assert.Equal(owned.Id, failure.Data["processId"]);
            Assert.Equal(true, failure.Data["cleanupProcessExited"]);
            Assert.Equal(60000, failure.Data["timeoutMs"]);
            var stdout = Assert.IsType<string>(failure.Data["stdoutTail"]);
            var stderr = Assert.IsType<string>(failure.Data["stderrTail"]);
            Assert.Contains("installer-fixture-ready", stdout);
            Assert.Contains("installer-fixture-stderr", stderr);
            Assert.True(stdout.Length <= 4096); Assert.True(stderr.Length <= 4096);
        }
        finally
        {
            // The before-fix regression must not leave the deliberately waiting child behind.
            if (owned is not null)
            {
                if (!owned.HasExited) owned.Kill(entireProcessTree: true);
                await owned.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                owned.Dispose();
            }
        }
    }

    [WindowsInstallerTheory]
    [InlineData(0)]
    [InlineData(23)]
    public async Task InstallerProcessPreservesCompletedOutputAndExitCode(int exitCode)
    {
        var result = await RunProcessAsync("powershell",
            ["-NoProfile", "-Command", $"[Console]::Out.WriteLine('installer-completed'); [Console]::Error.WriteLine('installer-diagnostic'); exit {exitCode}"],
            FindRepositoryRoot());
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Equal("installer-completed", result.StandardOutput.Trim());
        Assert.Equal("installer-diagnostic", result.StandardError.Trim());
    }

    [Fact]
    public async Task PackagedInstallerSupportsInstallRepairDoctorMcpAndUninstall()
    {
        var installerPath = Environment.GetEnvironmentVariable("AVASCOPE_INSTALLER_ARTIFACT");
        if (string.IsNullOrWhiteSpace(installerPath))
        {
            return;
        }

        installerPath = Path.GetFullPath(installerPath);
        Assert.True(File.Exists(installerPath), $"Expected installer artifact at {installerPath}.");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                installerPath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute);
        }

        var root = FindRepositoryRoot();
        var installRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"packaged-install-{Guid.NewGuid():N}");
        var unownedRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"unowned-install-{Guid.NewGuid():N}");
        var binDirectory = Path.Combine(installRoot, "bin");
        var commandPath = Path.Combine(binDirectory, OperatingSystem.IsWindows() ? "avascope.cmd" : "avascope");
        var expectedVersion = XDocument.Load(Path.Combine(root, "Directory.Build.props"))
            .Descendants("Version")
            .Single()
            .Value;
        string[] installerArguments = OperatingSystem.IsWindows()
            ?
            [
                "/VERYSILENT",
                "/SUPPRESSMSGBOXES",
                "/NORESTART",
                "/SP-",
                $"/DIR={installRoot}",
                "/TASKS="
            ]
            :
            [
                "--install-root",
                installRoot,
                "--bin-dir",
                binDirectory,
                "--no-path-update",
                "--no-registration"
            ];

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                var verifyResult = await RunProcessAsync(installerPath, ["--verify"], root);
                Assert.Equal(0, verifyResult.ExitCode);
                Assert.Contains("\"product\":\"AvaScope\"", verifyResult.StandardOutput, StringComparison.Ordinal);
                Assert.Contains("\"signed\":false", verifyResult.StandardOutput, StringComparison.Ordinal);
                Assert.Contains("\"notarized\":false", verifyResult.StandardOutput, StringComparison.Ordinal);
                Assert.Contains("\"trustModel\":\"unsigned-unnotarized\"", verifyResult.StandardOutput, StringComparison.Ordinal);

                if (OperatingSystem.IsMacOS())
                {
                    var unsafeSystemInstallResult = await RunProcessAsync(
                        installerPath,
                        [
                            "--install-root",
                            "/Applications/AvaScope",
                            "--bin-dir",
                            "/usr/local/bin",
                            "--no-path-update",
                            "--no-registration"
                        ],
                        root);
                    Assert.NotEqual(0, unsafeSystemInstallResult.ExitCode);
                    Assert.Contains("Unsafe install root", unsafeSystemInstallResult.StandardError, StringComparison.Ordinal);
                }

                Directory.CreateDirectory(unownedRoot);
                var unownedMarker = Path.Combine(unownedRoot, "keep.txt");
                await File.WriteAllTextAsync(unownedMarker, "not owned by AvaScope");
                var unsafeUninstallResult = await RunProcessAsync(
                    installerPath,
                    [
                        "--uninstall",
                        "--install-root",
                        unownedRoot,
                        "--bin-dir",
                        Path.Combine(unownedRoot, "bin"),
                        "--no-path-update",
                        "--no-registration"
                    ],
                    root);
                Assert.NotEqual(0, unsafeUninstallResult.ExitCode);
                Assert.True(File.Exists(unownedMarker), "Uninstall must preserve directories not owned by AvaScope.");
            }

            var installResult = await RunProcessAsync(installerPath, installerArguments, root);
            Assert.Equal(0, installResult.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(installResult.StandardError), installResult.StandardError);
            if (OperatingSystem.IsMacOS())
            {
                Assert.Contains("unsigned and unnotarized", installResult.StandardOutput, StringComparison.Ordinal);
                Assert.Contains("xattr -dr com.apple.quarantine", installResult.StandardOutput, StringComparison.Ordinal);
            }

            Assert.True(File.Exists(commandPath), $"Expected installed command at {commandPath}.");
            var discoveryPath = Path.Combine(installRoot, "avascope.discovery.json");
            string uninstallPath;
            using (var discovery = JsonDocument.Parse(await File.ReadAllTextAsync(discoveryPath)))
            {
                uninstallPath = discovery.RootElement.GetProperty("uninstallPath").GetString()!;
                Assert.False(string.IsNullOrWhiteSpace(uninstallPath));
                Assert.True(File.Exists(uninstallPath), $"Expected installed uninstaller at {uninstallPath}.");
            }

            foreach (var legalFileName in new[] { "LICENSE", "NOTICE", "LICENSE-SCOPE.md", "THIRD-PARTY-NOTICES.md" })
            {
                Assert.True(
                    File.Exists(Path.Combine(installRoot, "current", legalFileName)),
                    $"Expected installed legal file: {legalFileName}");
            }

            if (OperatingSystem.IsWindows())
            {
                var installedIconPath = Path.Combine(installRoot, "AvaScope.ico");
                Assert.True(File.Exists(installedIconPath), $"Expected installed application icon at {installedIconPath}.");

                var verificationPath = Path.Combine(binDirectory, "verify-avascope.cmd");
                Assert.True(File.Exists(verificationPath), $"Expected installed verification command at {verificationPath}.");
                var verificationResult = await RunProcessAsync(
                    verificationPath,
                    ["--no-pause"],
                    root);
                Assert.Equal(0, verificationResult.ExitCode);
                Assert.Contains("AVASCOPE SETUP CHECK", verificationResult.StandardOutput, StringComparison.Ordinal);
                Assert.Contains("[ SUCCESS ]", verificationResult.StandardOutput, StringComparison.Ordinal);
                Assert.Contains("\u001b[92m", verificationResult.StandardOutput, StringComparison.Ordinal);
                Assert.DoesNotContain("\u001b[91m", verificationResult.StandardOutput, StringComparison.Ordinal);
                Assert.Contains($"Installed version : {expectedVersion}", verificationResult.StandardOutput, StringComparison.Ordinal);

                var installedExecutablePath = Path.Combine(installRoot, "current", "avascope.exe");
                var hiddenExecutablePath = installedExecutablePath + ".verification-test";
                File.Move(installedExecutablePath, hiddenExecutablePath);
                try
                {
                    var failedVerificationResult = await RunProcessAsync(
                        verificationPath,
                        ["--no-pause"],
                        root);
                    Assert.NotEqual(0, failedVerificationResult.ExitCode);
                    Assert.Contains("[ FAILED ]", failedVerificationResult.StandardOutput, StringComparison.Ordinal);
                    Assert.Contains("\u001b[91m", failedVerificationResult.StandardOutput, StringComparison.Ordinal);
                    Assert.DoesNotContain("\u001b[92m", failedVerificationResult.StandardOutput, StringComparison.Ordinal);
                    Assert.Contains("Microsoft .NET 10 Runtime", failedVerificationResult.StandardOutput, StringComparison.Ordinal);
                }
                finally
                {
                    File.Move(hiddenExecutablePath, installedExecutablePath);
                }
            }

            var versionResult = await RunProcessAsync(commandPath, ["--version"], root);
            Assert.Equal(0, versionResult.ExitCode);
            Assert.Equal(expectedVersion, versionResult.StandardOutput.Trim());

            var doctorResult = await RunProcessAsync(
                commandPath,
                [
                    "doctor",
                    "--manifest-dir",
                    Path.Combine(installRoot, "sessions"),
                    "--preview-session-store",
                    Path.Combine(installRoot, "preview-sessions")
                ],
                root);
            Assert.Equal(0, doctorResult.ExitCode);
            Assert.Contains("\"status\":\"available\"", doctorResult.StandardOutput, StringComparison.Ordinal);

            var stderr = new List<string>();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                var environment = TestEnvironment.McpEnvironment();
                var installedMcpPath = Path.Combine(
                    installRoot,
                    "current",
                    OperatingSystem.IsWindows() ? "AvaScope.Mcp.exe" : "AvaScope.Mcp");
                await using var client = await McpClient.CreateAsync(
                    new StdioClientTransport(new StdioClientTransportOptions
                    {
                        Name = "AvaScope packaged installer",
                        Command = installedMcpPath,
                        WorkingDirectory = root,
                        InheritEnvironmentVariables = true,
                        EnvironmentVariables = environment,
                        ShutdownTimeout = TimeSpan.FromSeconds(5),
                        StandardErrorLines = stderr.Add
                    }),
                    cancellationToken: cancellation.Token);

                var tools = await client.ListToolsAsync(cancellationToken: cancellation.Token);
                Assert.Equal("avascope", client.ServerInfo.Name);
                Assert.Equal(AvaScopeProduct.Version, client.ServerInfo.Version);
                Assert.Contains(tools, static tool => tool.Name == "health");
            }

            var stalePath = Path.Combine(installRoot, "current", "stale-upgrade-sentinel.txt");
            await File.WriteAllTextAsync(stalePath, "stale");
            var repairResult = await RunProcessAsync(installerPath, installerArguments, root);
            Assert.Equal(0, repairResult.ExitCode);
            Assert.False(File.Exists(stalePath), "Repair/upgrade should replace the complete current payload.");

            var uninstallResult = OperatingSystem.IsWindows()
                ? await RunProcessAsync(
                    uninstallPath,
                    ["/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART"],
                    root)
                : await RunProcessAsync(
                    installerPath,
                    ["--uninstall", .. installerArguments],
                    root);
            Assert.Equal(0, uninstallResult.ExitCode);
            if (OperatingSystem.IsWindows())
            {
                await WaitForDirectoryDeletionAsync(installRoot);
            }
            Assert.False(Directory.Exists(installRoot), "Uninstall should remove the user-local install root.");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(installRoot);
            await DeleteDirectoryWithRetryAsync(unownedRoot);
        }
    }

    [Fact]
    public async Task InstallScriptCreatesStableCommandAndDiscoveryManifest()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = FindRepositoryRoot();
        var sourcePath = AppContext.BaseDirectory;
        var installRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"install-{Guid.NewGuid():N}");
        var expectedVersion = XDocument.Load(Path.Combine(root, "Directory.Build.props"))
            .Descendants("Version")
            .Single()
            .Value;

        try
        {
            var installResult = await RunProcessAsync(
                "powershell",
                [
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    Path.Combine(root, "eng", "install-avascope.ps1"),
                    "-SourcePath",
                    sourcePath,
                    "-InstallRoot",
                    installRoot,
                    "-SkipPathUpdate"
                ],
                root);

            Assert.Equal(0, installResult.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(installResult.StandardError), installResult.StandardError);

            var commandPath = Path.Combine(installRoot, "bin", "avascope.cmd");
            var manifestPath = Path.Combine(installRoot, "avascope.discovery.json");
            var executablePath = Path.Combine(installRoot, "current", "avascope.exe");

            Assert.True(File.Exists(commandPath), $"Expected command shim at {commandPath}.");
            Assert.True(File.Exists(manifestPath), $"Expected discovery manifest at {manifestPath}.");
            Assert.True(File.Exists(executablePath), $"Expected installed executable at {executablePath}.");

            var versionResult = await RunProcessAsync(
                commandPath,
                ["--version"],
                root);

            Assert.Equal(0, versionResult.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(versionResult.StandardError), versionResult.StandardError);
            Assert.Equal(expectedVersion, versionResult.StandardOutput.Trim());

            using var manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var manifest = manifestDocument.RootElement;
            Assert.Equal(1, manifest.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("AvaScope", manifest.GetProperty("product").GetString());
            Assert.Equal("avascope", manifest.GetProperty("serviceName").GetString());
            Assert.Equal(expectedVersion, manifest.GetProperty("version").GetString());
            Assert.Equal("per-user", manifest.GetProperty("installMode").GetString());
            Assert.Equal(Path.GetFullPath(installRoot), manifest.GetProperty("installRoot").GetString());
            Assert.Equal(Path.GetFullPath(commandPath), manifest.GetProperty("commandPath").GetString());
            Assert.Equal(Path.GetFullPath(executablePath), manifest.GetProperty("executablePath").GetString());

            var mcp = manifest.GetProperty("mcp");
            Assert.Equal("stdio", mcp.GetProperty("transport").GetString());
            Assert.Equal("avascope", mcp.GetProperty("serverName").GetString());
            Assert.Equal(Path.GetFullPath(commandPath), mcp.GetProperty("commandPath").GetString());
            Assert.Equal("mcp", mcp.GetProperty("arguments")[0].GetString());
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(installRoot);
        }
    }

    [Fact]
    public async Task InstalledCommandRunsMcpOverStdio()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = FindRepositoryRoot();
        var sourcePath = AppContext.BaseDirectory;
        var installRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"install-mcp-{Guid.NewGuid():N}");

        try
        {
            var installResult = await RunProcessAsync(
                "powershell",
                [
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    Path.Combine(root, "eng", "install-avascope.ps1"),
                    "-SourcePath",
                    sourcePath,
                    "-InstallRoot",
                    installRoot,
                    "-SkipPathUpdate"
                ],
                root);

            Assert.Equal(0, installResult.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(installResult.StandardError), installResult.StandardError);

            var commandPath = Path.Combine(installRoot, "bin", "avascope.cmd");
            var installedMcpPath = Path.Combine(installRoot, "current", "AvaScope.Mcp.exe");
            var stderr = new List<string>();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var environment = TestEnvironment.McpEnvironment();

            await using var client = await McpClient.CreateAsync(
                new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "AvaScope installed",
                    Command = installedMcpPath,
                    WorkingDirectory = root,
                    InheritEnvironmentVariables = false,
                    EnvironmentVariables = environment,
                    ShutdownTimeout = TimeSpan.FromSeconds(5),
                    StandardErrorLines = stderr.Add
                }),
                cancellationToken: cancellation.Token);

            var tools = await client.ListToolsAsync(cancellationToken: cancellation.Token);

            Assert.Equal("avascope", client.ServerInfo.Name);
            Assert.Equal(AvaScopeProduct.Version, client.ServerInfo.Version);
            Assert.Contains(tools, static tool => tool.Name == "health");
            Assert.Contains(tools, static tool => tool.Name == "preview_axaml");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(installRoot);
        }
    }

    private async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        Action<Process>? onStarted = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start());
        var timer = Stopwatch.StartNew();
        var phase = "process_started";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var captureCancellation = new CancellationTokenSource();
        var stdoutReader = process.StandardOutput;
        var stderrReader = process.StandardError;
        var streamsClosed = false;
        // Keep capture alive through owned termination so timeout does not discard partial output.
        var stdoutTask = stdoutReader.ReadToEndAsync(captureCancellation.Token);
        var stderrTask = stderrReader.ReadToEndAsync(captureCancellation.Token);
        try
        {
            onStarted?.Invoke(process);
            phase = "process_wait";
            await process.WaitForExitAsync(cancellation.Token);
            phase = "stdout_drain";
            var stdout = await stdoutTask.WaitAsync(cancellation.Token);
            phase = "stderr_drain";
            var stderr = await stderrTask.WaitAsync(cancellation.Token);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                operation = "installer_test_process", executable = Path.GetFileName(fileName),
                processId = process.Id, phase = "completed", process.ExitCode,
                elapsedMs = timer.Elapsed.TotalMilliseconds, timeoutMs = 60000
            }));
            return new ProcessResult(process.ExitCode, stdout, stderr);
        }
        catch (Exception primary)
        {
            var failedAfterMs = timer.Elapsed.TotalMilliseconds;
            var cleanupFailures = new List<string>();
            var exited = false;
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (Exception failure) { cleanupFailures.Add("terminate:" + failure.GetType().Name); }
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                exited = true;
            }
            catch (Exception failure) { cleanupFailures.Add("exit:" + failure.GetType().Name); }
            var stdout = await CaptureTailAsync(stdoutTask, "stdout");
            var stderr = await CaptureTailAsync(stderrTask, "stderr");
            captureCancellation.Cancel();
            CloseReader(stdoutReader, "stdout");
            CloseReader(stderrReader, "stderr");
            streamsClosed = true;
            var evidence = new Dictionary<string, object?>
            {
                ["operation"] = "installer_test_process", ["executable"] = Path.GetFileName(fileName),
                ["processId"] = process.Id, ["processPhase"] = phase,
                ["failureType"] = primary.GetType().Name, ["elapsedMs"] = failedAfterMs,
                ["timeoutMs"] = 60000, ["cleanupWaitMs"] = 3000, ["cleanupProcessExited"] = exited,
                ["cleanupFailures"] = cleanupFailures, ["stdoutTail"] = stdout, ["stderrTail"] = stderr
            };
            // These are observations, not a claim about which installer operation stalled.
            for (var index = 0; index + 1 < arguments.Count; index++)
            {
                if (!string.Equals(arguments[index], "-InstallRoot", StringComparison.OrdinalIgnoreCase)) continue;
                var installRoot = arguments[index + 1];
                evidence["stagingDirectoryExists"] = Directory.Exists(Path.Combine(installRoot, ".installing", "current"));
                evidence["stagedExecutableExists"] = File.Exists(Path.Combine(installRoot, ".installing", "current", "avascope.exe"));
                evidence["installedExecutableExists"] = File.Exists(Path.Combine(installRoot, "current", "avascope.exe"));
                evidence["shimExists"] = File.Exists(Path.Combine(installRoot, "bin", "avascope.cmd"));
                evidence["discoveryManifestExists"] = File.Exists(Path.Combine(installRoot, "avascope.discovery.json"));
                break;
            }
            foreach (var entry in evidence) primary.Data[entry.Key] = entry.Value;
            try { output.WriteLine(JsonSerializer.Serialize(evidence)); }
            catch (Exception failure) { primary.Data["evidenceWriteFailureType"] = failure.GetType().Name; }
            // Preserve the original cancellation/read/start-callback exception and stack.
            throw;

            async Task<string> CaptureTailAsync(Task<string> read, string stream)
            {
                try
                {
                    var text = await read.WaitAsync(TimeSpan.FromSeconds(3));
                    return text.Length <= 4096 ? text : text[^4096..];
                }
                catch (Exception failure)
                {
                    cleanupFailures.Add(stream + "_drain:" + failure.GetType().Name);
                    _ = read.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    return "[capture unavailable]";
                }
            }

            void CloseReader(StreamReader reader, string stream)
            {
                try { reader.Dispose(); }
                catch (Exception failure) { cleanupFailures.Add(stream + "_close:" + failure.GetType().Name); }
            }
        }
        finally
        {
            captureCancellation.Cancel();
            if (!streamsClosed)
            {
                try { stdoutReader.Dispose(); }
                finally { stderrReader.Dispose(); }
            }
        }
    }

    private static async Task DeleteDirectoryWithRetryAsync(string path)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                lastException = exception;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }

        throw new IOException($"Timed out deleting test directory '{path}'.", lastException);
    }

    private static async Task WaitForDirectoryDeletionAsync(string path)
    {
        for (var attempt = 0; attempt < 50 && Directory.Exists(path); attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AvaScope.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test output directory.");
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
