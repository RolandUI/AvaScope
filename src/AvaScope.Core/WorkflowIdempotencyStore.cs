using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

internal sealed class WorkflowIdempotencyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _directory;

    public WorkflowIdempotencyStore(string manifestDirectory)
    {
        _directory = Path.Combine(
            Path.GetFullPath(manifestDirectory),
            ".avascope-idempotency");
    }

    public async Task<CoreResult<IDisposable>> AcquireAsync(
        SessionId sessionId,
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_directory);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return CoreResult<IDisposable>.Fail(new CoreError(
                "semantic_workflow_idempotency_store_failed",
                $"Idempotency lease directory could not be created: {exception.Message}"));
        }

        var path = Path.Combine(
            _directory,
            $"{GetIdentityHash(sessionId, key)}.lock");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return CoreResult<IDisposable>.Ok(new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None));
            }
            catch (IOException)
            {
                await Task.Delay(25, cancellationToken);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException
                or NotSupportedException)
            {
                return CoreResult<IDisposable>.Fail(new CoreError(
                    "semantic_workflow_idempotency_store_failed",
                    $"Idempotency lease could not be acquired: {exception.Message}",
                    new Dictionary<string, string>
                    {
                        ["idempotencyKey"] = key,
                        ["leasePath"] = path
                    }));
            }
        }

        return CoreResult<IDisposable>.Fail(new CoreError(
            "semantic_workflow_idempotency_busy",
            "The idempotency key is currently being evaluated by another workflow.",
            new Dictionary<string, string>
            {
                ["idempotencyKey"] = key,
                ["leasePath"] = path
            }));
    }

    public CoreResult<SemanticWorkflowStepResult?> TryReplay(
        SessionId sessionId,
        string key,
        string signature)
    {
        var path = GetPath(sessionId, key);
        if (!File.Exists(path))
        {
            return CoreResult<SemanticWorkflowStepResult?>.Ok(null);
        }

        try
        {
            var record = JsonSerializer.Deserialize<WorkflowIdempotencyRecord>(
                File.ReadAllText(path),
                JsonOptions);
            if (record is null || record.Result is null)
            {
                return StoreFailure("Stored idempotency result is malformed.", path);
            }

            if (record.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                File.Delete(path);
                return CoreResult<SemanticWorkflowStepResult?>.Ok(null);
            }

            if (!string.Equals(record.Signature, signature, StringComparison.Ordinal))
            {
                return CoreResult<SemanticWorkflowStepResult?>.Fail(new CoreError(
                    "semantic_workflow_idempotency_conflict",
                    "The idempotency key was already used for a different workflow step.",
                    new Dictionary<string, string>
                    {
                        ["idempotencyKey"] = key,
                        ["createdAt"] = record.CreatedAt.ToString("O"),
                        ["expiresAt"] = record.ExpiresAt.ToString("O")
                    }));
            }

            var metadata = new Dictionary<string, string>(
                record.Result.Metadata,
                StringComparer.Ordinal)
            {
                ["idempotencyReplay"] = "true",
                ["idempotencyKey"] = key,
                ["originalExecutedAt"] = record.Result.ExecutedAt.ToString("O")
            };

            var original = record.Result;
            return CoreResult<SemanticWorkflowStepResult?>.Ok(new SemanticWorkflowStepResult(
                original.StepId,
                original.Action,
                original.Status,
                original.Message,
                original.ExecutedAt,
                original.Target,
                original.Input,
                original.Inspection,
                original.Screenshot,
                original.Diagnostics,
                metadata,
                original.Picker,
                original.Mutation));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return StoreFailure(exception.Message, path);
        }
    }

    public CoreResult<bool> Save(
        SessionId sessionId,
        string key,
        string signature,
        TimeSpan timeToLive,
        SemanticWorkflowStepResult result)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var now = DateTimeOffset.UtcNow;
            var path = GetPath(sessionId, key);
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            var record = new WorkflowIdempotencyRecord(
                signature,
                now,
                now.Add(timeToLive),
                result);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(record, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
            return CoreResult<bool>.Ok(true);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            return CoreResult<bool>.Fail(new CoreError(
                "semantic_workflow_idempotency_store_failed",
                $"Idempotency result could not be persisted: {exception.Message}",
                new Dictionary<string, string>
                {
                    ["idempotencyKey"] = key,
                    ["storeDirectory"] = _directory
                }));
        }
    }

    public static string CreateSignature(
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                sessionId = request.SessionId.Value,
                request.TopLevelId,
                Step = step
            },
            JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }

    private string GetPath(SessionId sessionId, string key)
    {
        return Path.Combine(_directory, $"{GetIdentityHash(sessionId, key)}.json");
    }

    private static string GetIdentityHash(SessionId sessionId, string key)
    {
        var identity = $"{sessionId.Value}\n{key}";
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
    }

    private static CoreResult<SemanticWorkflowStepResult?> StoreFailure(
        string message,
        string path) =>
        CoreResult<SemanticWorkflowStepResult?>.Fail(new CoreError(
            "semantic_workflow_idempotency_store_failed",
            $"Stored idempotency result could not be read: {message}",
            new Dictionary<string, string>
            {
                ["storePath"] = path
            }));

    private sealed record WorkflowIdempotencyRecord(
        string Signature,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        SemanticWorkflowStepResult Result);
}
