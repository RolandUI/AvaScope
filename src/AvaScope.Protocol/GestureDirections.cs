namespace AvaScope.Protocol;

public static class GestureDirections
{
    public const string Left = "left";
    public const string Right = "right";
    public const string Up = "up";
    public const string Down = "down";
    public const string Start = "start";
    public const string End = "end";

    public static IReadOnlyList<string> All { get; } =
    [
        Left, Right, Up, Down, Start, End
    ];
}
