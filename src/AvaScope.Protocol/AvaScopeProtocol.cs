namespace AvaScope.Protocol;

public static class AvaScopeProtocol
{
    public const string ServiceName = "avascope";

    public static ProtocolVersion CurrentVersion { get; } = new(1, 0);
}
