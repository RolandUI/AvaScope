using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewSessionStore
{
    private const string RootDirectoryName = "AvaScope";
    private const string SessionDirectoryName = "preview-sessions";
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
        return Path.Combine(Path.GetTempPath(), RootDirectoryName, SessionDirectoryName);
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
}
