using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

[JsonConverter(typeof(SessionIdJsonConverter))]
public sealed record SessionId
{
    public SessionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Session id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static SessionId New() => new(Guid.NewGuid().ToString("n"));

    public override string ToString() => Value;
}
