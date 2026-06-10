using System.Diagnostics;
using System.Globalization;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class BridgeAppLauncher
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public async Task<CoreResult<LaunchAppResponse>> LaunchAsync(
        string command,
        string? arguments = null,
        string? workingDirectory = null,
        string? displayName = null,
        string? manifestDirectory = null,
        string? outputDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return Fail("Launch command cannot be empty.");
        }

        var effectiveTimeout = timeout ?? DefaultTimeout;
        if (effectiveTimeout <= TimeSpan.Zero)
        {
            return Fail("Launch timeout must be positive.");
        }

        var fullManifestDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(manifestDirectory)
                ? BridgeSessionManifest.GetDefaultDirectory()
                : manifestDirectory);
        var fullOutputDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(Path.GetTempPath(), "AvaScope", "launches", Guid.NewGuid().ToString("n"))
                : outputDirectory);
        Directory.CreateDirectory(fullManifestDirectory);
        Directory.CreateDirectory(fullOutputDirectory);

        var stdoutPath = Path.Combine(fullOutputDirectory, "stdout.log");
        var stderrPath = Path.Combine(fullOutputDirectory, "stderr.log");
        File.WriteAllText(stdoutPath, string.Empty);
        File.WriteAllText(stderrPath, string.Empty);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.CurrentDirectory
                    : Path.GetFullPath(workingDirectory),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            },
            EnableRaisingEvents = true
        };

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    process.StartInfo.Environment[pair.Key] = pair.Value;
                }
            }
        }

        process.StartInfo.Environment[BridgeSessionManifest.DirectoryEnvironmentVariable] = fullManifestDirectory;

        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                return Fail("The configured app process could not be started.");
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            process.Dispose();
            return Fail($"The configured app process could not be started: {exception.Message}");
        }

        _ = CopyToFileUntilExitAsync(process.StandardOutput, stdoutPath);
        _ = CopyToFileUntilExitAsync(process.StandardError, stderrPath);

        var client = new LocalBridgeClient(fullManifestDirectory);
        var stopAt = DateTimeOffset.UtcNow + effectiveTimeout;
        while (DateTimeOffset.UtcNow < stopAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                return Fail(
                    "The launched app process exited before an AvaScope bridge session appeared.",
                    CreateLaunchDetails(process, stdoutPath, stderrPath, fullManifestDirectory, displayName, process.ExitCode));
            }

            var manifest = client
                .ListSessionManifests()
                .Where(session => session.ProcessId == process.Id)
                .OrderByDescending(session => session.CreatedAt)
                .FirstOrDefault();
            if (manifest is not null)
            {
                var attach = await client.AttachToAppAsync(
                    process.Id,
                    manifest.SessionId,
                    processName: null,
                    cancellationToken: cancellationToken);
                if (!attach.Success)
                {
                    return CoreResult<LaunchAppResponse>.Fail(attach.Error!);
                }

                var topLevels = await client.ListTopLevelsAsync(manifest.SessionId, cancellationToken);
                var topLevelId = topLevels.Success
                    ? topLevels.Value!.TopLevels.FirstOrDefault()?.Id
                    : null;

                return CoreResult<LaunchAppResponse>.Ok(new LaunchAppResponse(
                    attach.Value!.Session,
                    process.Id,
                    attach.Value.ProcessName ?? TryGetProcessName(process) ?? Path.GetFileNameWithoutExtension(command),
                    stdoutPath,
                    stderrPath,
                    startedAt,
                    DateTimeOffset.UtcNow,
                    topLevelId,
                    attach.Value.ManifestPath));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        return Fail(
            "Timed out waiting for an AvaScope bridge session from the launched app.",
            CreateLaunchDetails(process, stdoutPath, stderrPath, fullManifestDirectory, displayName));
    }

    private static async Task CopyToFileUntilExitAsync(StreamReader reader, string path)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            await using var writer = new StreamWriter(stream);
            while (await reader.ReadLineAsync() is { } line)
            {
                await writer.WriteLineAsync(line);
                await writer.FlushAsync();
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static IReadOnlyDictionary<string, string> CreateLaunchDetails(
        Process process,
        string stdoutPath,
        string stderrPath,
        string manifestDirectory,
        string? displayName,
        int? exitCode = null)
    {
        var details = new Dictionary<string, string>
        {
            ["processId"] = process.Id.ToString(CultureInfo.InvariantCulture),
            ["stdoutPath"] = Path.GetFullPath(stdoutPath),
            ["stderrPath"] = Path.GetFullPath(stderrPath),
            ["manifestDirectory"] = Path.GetFullPath(manifestDirectory),
            ["bridgeActivation"] = "explicit_app_opt_in_required",
            ["nextAction"] = $"Ensure the launched app enables AvaScopeBridge.Activate and writes a local manifest under {BridgeSessionManifest.DirectoryEnvironmentVariable}."
        };

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            details["displayName"] = displayName.Trim();
        }

        if (exitCode is not null)
        {
            details["exitCode"] = exitCode.Value.ToString(CultureInfo.InvariantCulture);
        }

        return details;
    }

    private static string? TryGetProcessName(Process process)
    {
        try
        {
            return process.HasExited ? null : process.ProcessName;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static CoreResult<LaunchAppResponse> Fail(
        string message,
        IReadOnlyDictionary<string, string>? details = null)
    {
        return CoreResult<LaunchAppResponse>.Fail(new CoreError(
            CoreErrorCodes.BridgeSessionNotFound,
            message,
            details));
    }
}
