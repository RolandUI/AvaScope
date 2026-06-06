using System.Reflection;
using System.Runtime.Versioning;

namespace AvaScope.Tests;

public sealed class ProjectFoundationTests
{
    private const string Net10TargetFramework = ".NETCoreApp,Version=v10.0";

    [Fact]
    public void ProtocolProjectAssemblyLoads()
    {
        AssertProjectAssemblyLoads("AvaScope.Protocol");
    }

    [Fact]
    public void CoreProjectAssemblyLoads()
    {
        AssertProjectAssemblyLoads("AvaScope.Core");
    }

    private static void AssertProjectAssemblyLoads(string assemblyName)
    {
        var assembly = Assembly.Load(new AssemblyName(assemblyName));

        Assert.Equal(assemblyName, assembly.GetName().Name);
        Assert.Equal(Net10TargetFramework, assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName);
    }
}
