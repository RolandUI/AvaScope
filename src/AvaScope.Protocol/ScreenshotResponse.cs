using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ScreenshotResponse
{
    [JsonConstructor]
    public ScreenshotResponse(
        SessionId sessionId,
        string topLevelId,
        string filePath,
        int pixelWidth,
        int pixelHeight,
        DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));
        }

        if (pixelWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), pixelWidth, "Pixel width must be positive.");
        }

        if (pixelHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), pixelHeight, "Pixel height must be positive.");
        }

        SessionId = sessionId;
        TopLevelId = topLevelId;
        FilePath = filePath;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        CapturedAt = capturedAt;
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("filePath")]
    public string FilePath { get; }

    [JsonPropertyName("pixelWidth")]
    public int PixelWidth { get; }

    [JsonPropertyName("pixelHeight")]
    public int PixelHeight { get; }

    [JsonPropertyName("capturedAt")]
    public DateTimeOffset CapturedAt { get; }
}
