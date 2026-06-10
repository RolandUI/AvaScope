using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeVector
{
    [JsonConstructor]
    public RuntimeVector(double x, double y)
    {
        X = x;
        Y = y;
    }

    [JsonPropertyName("x")]
    public double X { get; }

    [JsonPropertyName("y")]
    public double Y { get; }
}
