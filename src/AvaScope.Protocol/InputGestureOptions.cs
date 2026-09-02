using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record InputGestureOptions
{
    public const int MinimumDurationMs = 50;
    public const int MaximumDurationMs = 5000;

    [JsonConstructor]
    public InputGestureOptions(
        string? direction = null,
        double? distancePercentage = null,
        int? durationMs = null,
        string? destinationTargetNodeId = null)
    {
        if (distancePercentage is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(distancePercentage),
                distancePercentage,
                "Gesture distance percentage must be greater than 0 and at most 100.");
        }

        if (durationMs is < MinimumDurationMs or > MaximumDurationMs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMs),
                durationMs,
                $"Gesture duration must be between {MinimumDurationMs} and {MaximumDurationMs} ms.");
        }

        Direction = string.IsNullOrWhiteSpace(direction) ? null : direction.Trim();
        DistancePercentage = distancePercentage;
        DurationMs = durationMs;
        DestinationTargetNodeId = string.IsNullOrWhiteSpace(destinationTargetNodeId)
            ? null
            : destinationTargetNodeId.Trim();
    }

    [JsonPropertyName("direction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Direction { get; }

    [JsonPropertyName("distancePercentage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? DistancePercentage { get; }

    [JsonPropertyName("durationMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DurationMs { get; }

    [JsonPropertyName("destinationTargetNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DestinationTargetNodeId { get; }
}
