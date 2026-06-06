using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record NodeBounds
{
    [JsonConstructor]
    public NodeBounds(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    [JsonPropertyName("x")]
    public double X { get; }

    [JsonPropertyName("y")]
    public double Y { get; }

    [JsonPropertyName("width")]
    public double Width { get; }

    [JsonPropertyName("height")]
    public double Height { get; }
}
