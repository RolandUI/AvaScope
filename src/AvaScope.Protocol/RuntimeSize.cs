using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeSize
{
    [JsonConstructor]
    public RuntimeSize(double width, double height)
    {
        Width = width;
        Height = height;
    }

    [JsonPropertyName("width")]
    public double Width { get; }

    [JsonPropertyName("height")]
    public double Height { get; }
}
