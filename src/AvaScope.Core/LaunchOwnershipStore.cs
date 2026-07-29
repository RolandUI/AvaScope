using System.Diagnostics;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

internal sealed record LaunchOwnershipRecord(
    SessionSummary Session,
    int ProcessId,
    string ProcessName,
    DateTimeOffset ProcessStartedAt,
    DateTimeOffset LaunchedAt);

internal sealed class LaunchOwnershipStore
{
    private const string DirectoryName = "launch-ownership";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _directory;

    public LaunchOwnershipStore(string manifestDirectory)
    {
        _directory = Path.Combine(Path.GetFullPath(manifestDirectory), DirectoryName);
    }

    public void Save(LaunchOwnershipRecord record)
    {
        Directory.CreateDirectory(_directory);
        var path = GetPath(record.Session.SessionId);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(record, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }

    public LaunchOwnershipRecord? TryRead(SessionId sessionId)
    {
        var path = GetPath(sessionId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LaunchOwnershipRecord>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static bool TryGetProcessIdentity(int processId, out Process process, out DateTimeOffset startedAt)
    {
        process = null!;
        startedAt = default;
        try
        {
            process = Process.GetProcessById(processId);
            startedAt = process.StartTime.ToUniversalTime();
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            process?.Dispose();
            return false;
        }
        catch (InvalidOperationException)
        {
            process?.Dispose();
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            process?.Dispose();
            return false;
        }
    }

    public static bool IsSameProcess(DateTimeOffset expected, DateTimeOffset actual)
    {
        return Math.Abs((expected - actual).TotalSeconds) < 1;
    }

    private string GetPath(SessionId sessionId)
    {
        return Path.Combine(_directory, $"{sessionId.Value}.json");
    }
}
