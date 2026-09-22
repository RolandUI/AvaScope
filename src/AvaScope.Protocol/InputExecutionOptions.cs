using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

/// <summary>Explicit input routing and bounded compound input. Omission preserves legacy routing.</summary>
public sealed record InputExecutionOptions
{
    [JsonPropertyName("strategy")] public string Strategy { get; init; } = "synthetic";
    [JsonPropertyName("button")] public string Button { get; init; } = "left";
    [JsonPropertyName("clickCount")] public int ClickCount { get; init; } = 1;
    [JsonPropertyName("intervalMs")] public int IntervalMs { get; init; } = 75;
    [JsonPropertyName("durationMs")] public int DurationMs { get; init; } = 250;
    [JsonPropertyName("motionSteps")] public int MotionSteps { get; init; } = 10;
    [JsonPropertyName("motionProfile")] public string MotionProfile { get; init; } = "linear";
    [JsonPropertyName("destinationX")] public double? DestinationX { get; init; }
    [JsonPropertyName("destinationY")] public double? DestinationY { get; init; }
    [JsonPropertyName("keys")] public IReadOnlyList<InputKeyStroke> Keys { get; init; } = [];

    public string? GetValidationError(string action)
    {
        if (Strategy is not ("semantic" or "synthetic" or "native") || Button is not ("left" or "right" or "middle")
            || ClickCount is < 1 or > 3 || IntervalMs is < 0 or > 250 || DurationMs is < 0 or > 3000
            || MotionSteps is < 1 or > 120 || MotionProfile is not ("linear" or "ease_in_out")
            || Keys is null || Keys.Count > 32 || Keys.Count * IntervalMs > 3000)
            return "Invalid input options: strategy semantic/synthetic/native, button left/right/middle, clickCount 1..3, intervalMs 0..250, durationMs 0..3000, motionSteps 1..120, profile linear/ease_in_out, at most 32 keys and 3000 ms total sequence delay.";
        if ((action == InputActions.KeySequence) != (Keys.Count > 0)) return "key_sequence requires execution.keys; other actions do not accept keys.";
        if (Keys.Any(stroke => stroke is null || string.IsNullOrWhiteSpace(stroke.Key) || stroke.Key.Length > 64 || stroke.Modifiers?.Length > 64))
            return "Key entries require nonempty names and modifier strings of at most 64 characters.";
        if (Strategy == "semantic" && action is not (InputActions.Click or InputActions.KeyText or InputActions.ClearText or InputActions.Focus
            or InputActions.Invoke or InputActions.Select or InputActions.Toggle or InputActions.Expand or InputActions.Collapse or InputActions.Scroll))
            return "The requested action has no semantic route.";
        if (Strategy != "semantic" && action is not (InputActions.Click or InputActions.KeyText or InputActions.KeySequence or InputActions.PointerMove or InputActions.Drag))
            return "Explicit synthetic/native input supports click, pointer_move, drag, key_sequence and key_text.";
        if (action != InputActions.Click && (ClickCount != 1 || (Button != "left" && action != InputActions.Drag)))
            return "Click grouping/button options require click or drag.";
        if ((DestinationX is null) != (DestinationY is null) || (DestinationX is { } x && !double.IsFinite(x)) || (DestinationY is { } y && !double.IsFinite(y)))
            return "Both finite destinationX and destinationY are required together.";
        if (action == InputActions.Drag && DestinationX is null) return "Explicit drag requires destinationX/Y in the selected top-level's DIP coordinates.";
        if (DestinationX is not null && action is not (InputActions.Drag or InputActions.PointerMove)) return "Only pointer_move and drag accept a motion destination.";
        return null;
    }
}

public sealed record InputKeyStroke(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("modifiers")] string? Modifiers = null);
