namespace AvaScope.Bridge;

public interface IAvaScopeDebugStateProvider
{
    IReadOnlyDictionary<string, string?> GetAvaScopeDebugState();
}
