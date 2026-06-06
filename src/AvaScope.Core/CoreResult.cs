namespace AvaScope.Core;

public sealed record CoreResult<T>
{
    private CoreResult(bool success, T? value, CoreError? error)
    {
        Success = success;
        Value = value;
        Error = error;
    }

    public bool Success { get; }

    public T? Value { get; }

    public CoreError? Error { get; }

    public static CoreResult<T> Ok(T value) => new(true, value, null);

    public static CoreResult<T> Fail(CoreError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new CoreResult<T>(false, default, error);
    }
}
