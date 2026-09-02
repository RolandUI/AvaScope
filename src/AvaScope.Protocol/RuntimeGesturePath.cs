using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeGesturePath
{
    [JsonConstructor]
    public RuntimeGesturePath(
        NodeBounds sourceBounds,
        IReadOnlyList<RuntimeVector> points,
        string coordinateSpace,
        NodeBounds? destinationBounds = null,
        string? direction = null,
        double? distancePercentage = null,
        bool clipped = false)
    {
        SourceBounds = sourceBounds ?? throw new ArgumentNullException(nameof(sourceBounds));
        Points = points ?? throw new ArgumentNullException(nameof(points));
        if (points.Count == 0)
        {
            throw new ArgumentException("Gesture path must contain at least one point.", nameof(points));
        }

        if (string.IsNullOrWhiteSpace(coordinateSpace))
        {
            throw new ArgumentException("Gesture coordinate space cannot be empty.", nameof(coordinateSpace));
        }

        DestinationBounds = destinationBounds;
        CoordinateSpace = coordinateSpace.Trim();
        Direction = string.IsNullOrWhiteSpace(direction) ? null : direction.Trim();
        DistancePercentage = distancePercentage;
        Clipped = clipped;
    }

    [JsonPropertyName("sourceBounds")]
    public NodeBounds SourceBounds { get; }

    [JsonPropertyName("destinationBounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeBounds? DestinationBounds { get; }

    [JsonPropertyName("points")]
    public IReadOnlyList<RuntimeVector> Points { get; }

    [JsonPropertyName("coordinateSpace")]
    public string CoordinateSpace { get; }

    [JsonPropertyName("direction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Direction { get; }

    [JsonPropertyName("distancePercentage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? DistancePercentage { get; }

    [JsonPropertyName("clipped")]
    public bool Clipped { get; }
}
