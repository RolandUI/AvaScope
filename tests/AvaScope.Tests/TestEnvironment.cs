using System.Runtime.CompilerServices;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

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
        if (Environment.GetEnvironmentVariable(BridgeSessionManifest.DirectoryEnvironmentVariable) is null)
            Environment.SetEnvironmentVariable(BridgeSessionManifest.DirectoryEnvironmentVariable,
                Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "sessions-" + Guid.NewGuid().ToString("N")));
    }

    internal static Dictionary<string, string?> McpEnvironment()
    {
        var environment = new Dictionary<string, string?>(StdioClientTransportOptions.GetDefaultEnvironmentVariables());
        environment[AgentRunStore.DirectoryEnvironmentVariable] = Environment.GetEnvironmentVariable(AgentRunStore.DirectoryEnvironmentVariable);
        environment[BridgeSessionManifest.DirectoryEnvironmentVariable] = Environment.GetEnvironmentVariable(BridgeSessionManifest.DirectoryEnvironmentVariable);
        return environment;
    }
}
