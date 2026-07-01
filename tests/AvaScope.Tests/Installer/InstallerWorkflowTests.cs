using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Installer;

public sealed class InstallerWorkflowTests
{
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
            var stderr = new List<string>();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();

            await using var client = await McpClient.CreateAsync(
                new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "AvaScope installed",
                    Command = commandPath,
                    Arguments = ["mcp"],
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

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellation.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellation.Token);
        await process.WaitForExitAsync(cancellation.Token);
        return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
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
