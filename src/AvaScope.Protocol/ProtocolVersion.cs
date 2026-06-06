using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ProtocolVersion
{
    [JsonConstructor]
    public ProtocolVersion(int major, int minor)
    {
        if (major < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(major), major, "Major version must be positive.");
        }

        if (minor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minor), minor, "Minor version cannot be negative.");
        }

        Major = major;
        Minor = minor;
    }

    [JsonPropertyName("major")]
    public int Major { get; }

    [JsonPropertyName("minor")]
    public int Minor { get; }

    public override string ToString() => $"{Major}.{Minor}";
}
