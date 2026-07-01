using System.Reflection;

namespace AvaScope.Protocol;

public static class AvaScopeProduct
{
    public const string ProductName = "AvaScope";

    public static string Version { get; } = GetAssemblyVersion(typeof(AvaScopeProduct).Assembly);

    private static string GetAssemblyVersion(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        return informationalVersion?.Split('+', 2)[0]
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
    }
}
