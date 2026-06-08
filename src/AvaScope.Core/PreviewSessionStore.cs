using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewSessionStore
{
    public const string DirectoryEnvironmentVariable = "AVASCOPE_PREVIEW_SESSION_STORE";
    private const string RootDirectoryName = "AvaScope";
    private const string SessionDirectoryName = "preview-sessions";
    private const int MaximumDiagnosticRecords = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PreviewSessionStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Preview session store directory cannot be empty.", nameof(directory));
        }

        Directory = Path.GetFullPath(directory);
    }

    public string Directory { get; }

    public static PreviewSessionStore CreateDefault()
    {
        return new PreviewSessionStore(GetDefaultDirectory());
    }

    public static string GetDefaultDirectory()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(Path.GetTempPath(), RootDirectoryName, SessionDirectoryName)
            : configuredDirectory;
    }

    public IReadOnlyList<PreviewSessionSummary> Load()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }

        var sessions = new List<PreviewSessionSummary>();
        foreach (var path in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
        {
            try
            {
                var session = JsonSerializer.Deserialize<PreviewSessionSummary>(
                    File.ReadAllText(path, Encoding.UTF8),
                    JsonOptions);
                if (session is not null)
                {
                    sessions.Add(session);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                // Ignore corrupt records; a bad persisted preview session must not prevent server startup.
            }
        }

        return sessions
            .OrderBy(static session => session.Session.CreatedAt)
            .ToArray();
    }

    public IReadOnlyList<PreviewSessionDiagnostic> GetDiagnostics()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }

        return System.IO.Directory
            .EnumerateFiles(Directory, "*.json")
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumDiagnosticRecords)
            .Select(DiagnoseRecord)
            .ToArray();
    }

    public CoreResult<PreviewCleanupResponse> CleanupStale()
    {
        var diagnostics = GetDiagnostics()
            .Where(static diagnostic => diagnostic.Status is DiagnosticStatuses.Stale or DiagnosticStatuses.Invalid)
            .ToArray();
        var deletedPaths = new List<string>();

        foreach (var diagnostic in diagnostics)
        {
            var recordPath = Path.GetFullPath(diagnostic.RecordPath);
            if (!IsPathInsideStore(recordPath))
            {
                continue;
            }

            try
            {
                if (File.Exists(recordPath))
                {
                    File.Delete(recordPath);
                    deletedPaths.Add(recordPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return CoreResult<PreviewCleanupResponse>.Fail(new CoreError(
                    CoreErrorCodes.PreviewSessionStoreFailed,
                    $"Preview session cleanup failed: {exception.Message}"));
            }
        }

        return CoreResult<PreviewCleanupResponse>.Ok(new PreviewCleanupResponse(
            Directory,
            deletedPaths.Count,
            diagnostics,
            deletedPaths,
            DateTimeOffset.UtcNow));
    }

    public CoreResult<bool> Save(PreviewSessionSummary session)
    {
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var path = GetPath(session.Session.SessionId);
            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(session, JsonOptions), Encoding.UTF8);
            File.Move(tempPath, path, overwrite: true);
            return CoreResult<bool>.Ok(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return CoreResult<bool>.Fail(new CoreError(
                CoreErrorCodes.PreviewSessionStoreFailed,
                $"Preview session store write failed: {exception.Message}"));
        }
    }

    private string GetPath(SessionId sessionId)
    {
        return Path.Combine(Directory, $"{EncodeFileName(sessionId.Value)}.json");
    }

    private static string EncodeFileName(string value)
    {
        return Convert.ToHexString(Encoding.UTF8.GetBytes(value));
    }

    private PreviewSessionDiagnostic DiagnoseRecord(string path)
    {
        try
        {
            var session = JsonSerializer.Deserialize<PreviewSessionSummary>(
                File.ReadAllText(path, Encoding.UTF8),
                JsonOptions);
            if (session is null)
            {
                return InvalidRecord(path, "Preview session record did not contain a session object.");
            }

            var outputPath = session.LastRender.Success ? session.LastRender.Value?.FilePath : null;
            if (session.Session.State is SessionStates.Closed or SessionStates.Failed)
            {
                return new PreviewSessionDiagnostic(
                    DiagnosticStatuses.Stale,
                    path,
                    session.Session,
                    outputPath,
                    new ProtocolError(
                        CoreErrorCodes.PreviewSessionStoreFailed,
                        $"Preview session record is {session.Session.State}."));
            }

            if (!string.IsNullOrWhiteSpace(outputPath) && !File.Exists(outputPath))
            {
                return new PreviewSessionDiagnostic(
                    DiagnosticStatuses.Stale,
                    path,
                    session.Session,
                    outputPath,
                    new ProtocolError(
                        CoreErrorCodes.PreviewSessionStoreFailed,
                        "Preview session output file is missing."));
            }

            return new PreviewSessionDiagnostic(
                DiagnosticStatuses.Available,
                path,
                session.Session,
                outputPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return InvalidRecord(path, exception.Message);
        }
    }

    private static PreviewSessionDiagnostic InvalidRecord(string path, string message)
    {
        return new PreviewSessionDiagnostic(
            DiagnosticStatuses.Invalid,
            path,
            error: new ProtocolError(CoreErrorCodes.PreviewSessionStoreFailed, message));
    }

    private bool IsPathInsideStore(string path)
    {
        var directory = Path.GetFullPath(Directory);
        return path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
