using ModelContextProtocol.Client;
using AvaScope.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;

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

        Assert.Equal("avascope", client.ServerInfo.Name);
        Assert.Equal(AvaScopeProduct.Version, client.ServerInfo.Version);
        Assert.Contains("health", toolNames);
        Assert.Contains("capabilities", toolNames);
        Assert.Contains("list_sessions", toolNames);
        Assert.Contains("close_session", toolNames);
        Assert.Contains("diagnostics", toolNames);
        Assert.Contains("mutate_node", toolNames);
        Assert.Contains("mutate_node_evidence", toolNames);
        Assert.Contains("mutation_review", toolNames);
        Assert.Contains("audit_ui", toolNames);
        Assert.Contains("design_quality_audit", toolNames);
        Assert.Contains("explain_layout", toolNames);
        Assert.Contains("run_workflow", toolNames);
        Assert.Contains("run_scenario", toolNames);
        Assert.Contains("pointer_diagnostics", toolNames);
        Assert.Contains("pseudo_state_matrix", toolNames);
        Assert.Contains("record_interaction_animation", toolNames);
        Assert.Contains("preview_axaml", toolNames);
        Assert.Contains("preview_axaml_multi", toolNames);
        Assert.Contains("preview_axaml_animation", toolNames);
        Assert.Contains("baseline_check", toolNames);
        Assert.Contains("semantic_diff", toolNames);
        Assert.Contains("create_preview_session", toolNames);
        Assert.Contains("list_preview_sessions", toolNames);
        Assert.Contains("preview_viewer", toolNames);
        Assert.Contains("close_preview_session", toolNames);
        Assert.Contains("reload", toolNames);
        Assert.Contains("cleanup", toolNames);
        Assert.Contains("cleanup_bridge_sessions", toolNames);

        var input = Assert.Single(tools, static tool => tool.Name == "input");
        Assert.Contains("derives the center", input.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicit coordinates take precedence", input.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        var inputSchema = JsonSerializer.SerializeToNode(input.ProtocolTool.InputSchema)!.AsObject();
        var actionDescription = inputSchema["properties"]!["action"]!["description"]!.GetValue<string>();
        Assert.Contains("invoke", actionDescription, StringComparison.Ordinal);
        Assert.Contains("select", actionDescription, StringComparison.Ordinal);
        Assert.Contains("toggle", actionDescription, StringComparison.Ordinal);
        Assert.Contains("expand", actionDescription, StringComparison.Ordinal);
        Assert.Contains("collapse", actionDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdvertisedRequiredOutputFieldsArePresentInSuccessAndFailureResponses()
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
        var healthTool = Assert.Single(tools, static tool => tool.Name == "health");
        var closeSessionTool = Assert.Single(tools, static tool => tool.Name == "close_session");
        AssertSchemaEnum(
            Assert.Single(tools, static tool => tool.Name == "input").ProtocolTool.InputSchema,
            "action",
            InputActions.All);
        AssertSchemaEnum(
            Assert.Single(tools, static tool => tool.Name == "diagnostics").ProtocolTool.InputSchema,
            "mode",
            ["all", "active-only", "minimal", "json-minimal"]);
        AssertSchemaEnum(
            Assert.Single(tools, static tool => tool.Name == "mutate_node").ProtocolTool.InputSchema,
            "operation",
            [
                RuntimeMutationOperationKinds.NoOp,
                RuntimeMutationOperationKinds.SetProperty,
                RuntimeMutationOperationKinds.AddClass,
                RuntimeMutationOperationKinds.RemoveClass,
                RuntimeMutationOperationKinds.ToggleClass,
                RuntimeMutationOperationKinds.SetResource,
                RuntimeMutationOperationKinds.RemoveResource,
                RuntimeMutationOperationKinds.ResetMutation,
                RuntimeMutationOperationKinds.ResetAll
            ]);

        var healthResult = await client.CallToolAsync(
            "health",
            new Dictionary<string, object?>(),
            cancellationToken: cancellation.Token);
        var closeSessionResult = await client.CallToolAsync(
            "close_session",
            new Dictionary<string, object?>
            {
                ["sessionId"] = "rider-schema-validation-missing-session"
            },
            cancellationToken: cancellation.Token);

        AssertRequiredFieldsPresent(healthTool.ProtocolTool.OutputSchema, healthResult.StructuredContent);
        AssertRequiredFieldsPresent(closeSessionTool.ProtocolTool.OutputSchema, closeSessionResult.StructuredContent);

        var success = JsonSerializer.SerializeToNode(healthResult.StructuredContent)!.AsObject();
        Assert.True(success["success"]!.GetValue<bool>());
        Assert.True(success.ContainsKey("error"));
        Assert.Null(success["error"]);

        var failure = JsonSerializer.SerializeToNode(closeSessionResult.StructuredContent)!.AsObject();
        Assert.False(failure["success"]!.GetValue<bool>());
        Assert.True(failure.ContainsKey("value"));
        Assert.Null(failure["value"]);
        Assert.NotNull(failure["error"]);
    }

    private static void AssertRequiredFieldsPresent(JsonElement? outputSchema, JsonElement? structuredContent)
    {
        var schema = JsonSerializer.SerializeToNode(outputSchema)!.AsObject();
        var response = JsonSerializer.SerializeToNode(structuredContent)!.AsObject();
        var required = schema["required"]!.AsArray()
            .Select(static field => field!.GetValue<string>());

        foreach (var field in required)
        {
            Assert.True(response.ContainsKey(field), $"Required output field '{field}' was omitted.");
        }
    }

    private static void AssertSchemaEnum(
        JsonElement? inputSchema,
        string propertyName,
        IReadOnlyList<string> expected)
    {
        var schema = JsonSerializer.SerializeToNode(inputSchema)!.AsObject();
        var values = schema["properties"]![propertyName]!["enum"]!.AsArray()
            .Select(static value => value!.GetValue<string>())
            .ToArray();
        Assert.Equal(expected, values);
    }
}
