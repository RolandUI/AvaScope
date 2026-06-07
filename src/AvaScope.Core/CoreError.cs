namespace AvaScope.Core;

public sealed record CoreError
{
    public CoreError(string code, string message, IReadOnlyDictionary<string, string>? details = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Error code cannot be empty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Error message cannot be empty.", nameof(message));
        }

        Code = code;
        Message = message;
        Details = details;
    }

    public string Code { get; }

    public string Message { get; }

    public IReadOnlyDictionary<string, string>? Details { get; }
}
