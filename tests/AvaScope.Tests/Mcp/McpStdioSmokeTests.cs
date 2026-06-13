using ModelContextProtocol.Client;

namespace AvaScope.Tests.Mcp;

public sealed class McpStdioSmokeTests
{
    [Fact]
    public async Task ServerStartsOverStdioAndListsInitialTools()
    {
        var serverAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll");
        Assert.True(File.Exists(serverAssembly), $"Expected MCP server assembly at {serverAssembly}.");

        var stderr = new List<string>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();

        await using var client = await McpClient.CreateAsync(
            new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "AvaScope",
                Command = "dotnet",
                Arguments = [serverAssembly],
                WorkingDirectory = AppContext.BaseDirectory,
                InheritEnvironmentVariables = false,
                EnvironmentVariables = environment,
                ShutdownTimeout = TimeSpan.FromSeconds(5),
                StandardErrorLines = stderr.Add
            }),
            cancellationToken: cancellation.Token);

        var tools = await client.ListToolsAsync(cancellationToken: cancellation.Token);
        var toolNames = tools.Select(static tool => tool.Name).ToArray();

        Assert.Contains("health", toolNames);
        Assert.Contains("capabilities", toolNames);
        Assert.Contains("list_sessions", toolNames);
        Assert.Contains("close_session", toolNames);
        Assert.Contains("diagnostics", toolNames);
        Assert.Contains("mutate_node", toolNames);
        Assert.Contains("mutate_node_evidence", toolNames);
        Assert.Contains("mutation_review", toolNames);
        Assert.Contains("audit_ui", toolNames);
        Assert.Contains("preview_axaml", toolNames);
        Assert.Contains("preview_axaml_multi", toolNames);
        Assert.Contains("preview_axaml_animation", toolNames);
        Assert.Contains("baseline_check", toolNames);
        Assert.Contains("create_preview_session", toolNames);
        Assert.Contains("list_preview_sessions", toolNames);
        Assert.Contains("preview_viewer", toolNames);
        Assert.Contains("close_preview_session", toolNames);
        Assert.Contains("reload", toolNames);
        Assert.Contains("cleanup", toolNames);
        Assert.Contains("cleanup_bridge_sessions", toolNames);
    }
}
