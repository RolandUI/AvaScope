using System.Runtime.CompilerServices;
using AvaScope.Core;

namespace AvaScope.Tests;

internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Child CLI/MCP processes inherit the same isolated recovery registry.
        // Tests must not scan or reserve paths in the developer's real run store.
        if (Environment.GetEnvironmentVariable(AgentRunStore.DirectoryEnvironmentVariable) is null)
            Environment.SetEnvironmentVariable(AgentRunStore.DirectoryEnvironmentVariable,
                Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "run-store-" + Guid.NewGuid().ToString("N")));
    }
}
