using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ScreenshotRegion
{
    [JsonConstructor]
    public ScreenshotRegion(int x, int y, int width, int height, string? name = null)
    {
        if (x < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "Region x cannot be negative.");
        }

        if (y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, "Region y cannot be negative.");
        }

        if (width < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Region width must be positive.");
        }

        if (height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Region height must be positive.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
        Name = string.IsNullOrWhiteSpace(name) ? null : name;
    }

    [JsonPropertyName("x")]
    public int X { get; }

    [JsonPropertyName("y")]
    public int Y { get; }

    [JsonPropertyName("width")]
    public int Width { get; }

    [JsonPropertyName("height")]
    public int Height { get; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; }
}
