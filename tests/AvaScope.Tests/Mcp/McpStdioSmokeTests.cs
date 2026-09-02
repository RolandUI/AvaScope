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
        var serverAssembly = ResolveServerAssembly();
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
        Assert.Contains("session_capabilities", toolNames);
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
        Assert.Contains("derive coordinates", input.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("destination target", input.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        var inputSchema = JsonSerializer.SerializeToNode(input.ProtocolTool.InputSchema)!.AsObject();
        var actionDescription = inputSchema["properties"]!["action"]!["description"]!.GetValue<string>();
        Assert.Contains("invoke", actionDescription, StringComparison.Ordinal);
        Assert.Contains("select", actionDescription, StringComparison.Ordinal);
        Assert.Contains("toggle", actionDescription, StringComparison.Ordinal);
        Assert.Contains("expand", actionDescription, StringComparison.Ordinal);
        Assert.Contains("collapse", actionDescription, StringComparison.Ordinal);
        Assert.Contains("drag", actionDescription, StringComparison.Ordinal);
        Assert.Contains("swipe", actionDescription, StringComparison.Ordinal);
        Assert.NotNull(inputSchema["properties"]!["gestureDirection"]);
        Assert.NotNull(inputSchema["properties"]!["gestureDistancePercentage"]);
        Assert.NotNull(inputSchema["properties"]!["gestureDurationMs"]);
        Assert.NotNull(inputSchema["properties"]!["destinationTargetNodeId"]);
        var workflow = Assert.Single(tools, static tool => tool.Name == "run_workflow");
        Assert.Contains("rendered", workflow.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("binding", workflow.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("top-level", workflow.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aliases", workflow.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("if/else", workflow.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retry_until", workflow.ProtocolTool.Description, StringComparison.Ordinal);
        Assert.Contains("validateOnly", workflow.ProtocolTool.Description, StringComparison.Ordinal);
        Assert.Contains("postcondition", workflow.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JUnit", workflow.ProtocolTool.Description, StringComparison.Ordinal);
        Assert.Contains("redaction", workflow.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("network upload unavailable", workflow.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "\"waitCondition\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.InputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"waitObservation\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.OutputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"topLevelAliases\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.InputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"resolvedTopLevelId\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.OutputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"fragments\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.InputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"maxAttempts\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.InputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"validateOnly\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.InputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"verify\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.InputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"evidence\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.InputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"ownedEvidenceRoot\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.InputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"allowedActions\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.InputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"executionPath\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.OutputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"plan\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.OutputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"verification\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.OutputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"failureEvidence\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.OutputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"reportPack\"",
            JsonSerializer.Serialize(workflow.ProtocolTool.OutputSchema),
            StringComparison.Ordinal);
        var scenario = Assert.Single(tools, static tool => tool.Name == "run_scenario");
        Assert.Contains("before launch", scenario.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("execution paths", scenario.ProtocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "\"fragments\"",
            JsonSerializer.Serialize(scenario.ProtocolTool.InputSchema),
            StringComparison.Ordinal);
        Assert.Contains(
            "\"workflowTimeoutMs\"",
            JsonSerializer.Serialize(scenario.ProtocolTool.InputSchema),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdvertisedRequiredOutputFieldsArePresentInSuccessAndFailureResponses()
    {
        var serverAssembly = ResolveServerAssembly();
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
            RuntimeMutationOperationKinds.All);
        foreach (var toolName in new[] { "inspect_node", "explain_layout", "find_nodes", "audit_ui", "mutate_node", "mutate_node_evidence" })
        {
            AssertSchemaEnum(
                Assert.Single(tools, tool => tool.Name == toolName).ProtocolTool.InputSchema,
                "treeKind",
                [TreeKinds.Visual, TreeKinds.Logical]);
        }

        AssertSchemaEnum(
            Assert.Single(tools, static tool => tool.Name == "mutate_node_evidence").ProtocolTool.InputSchema,
            "operation",
            RuntimeMutationOperationKinds.All);
        AssertSchemaEnum(
            Assert.Single(tools, static tool => tool.Name == "native_picker").ProtocolTool.InputSchema,
            "operation",
            NativePickerOperations.All);
        AssertSchemaEnum(
            Assert.Single(tools, static tool => tool.Name == "native_picker").ProtocolTool.InputSchema,
            "predefinedResult",
            NativePickerResultStates.Preparable);
        foreach (var toolName in new[]
                 {
                     "preview_axaml",
                     "preview_axaml_multi",
                     "preview_axaml_animation",
                     "create_preview_session"
                 })
        {
            var schema = Assert.Single(tools, tool => tool.Name == toolName).ProtocolTool.InputSchema;
            AssertSchemaEnum(schema, "minimumSeverity", PreviewMinimumSeverities.Values);
            var properties = JsonSerializer.SerializeToNode(schema)!.AsObject()["properties"]!.AsObject();
            Assert.True(properties.ContainsKey("errorsOnly"));
            Assert.True(properties.ContainsKey("diagnosticsBaselinePath"));
            Assert.True(properties.ContainsKey("diagnosticsBaselineFingerprints"));
        }

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

    private static string ResolveServerAssembly()
    {
        var packagedAssembly = Environment.GetEnvironmentVariable("AVASCOPE_PACKAGED_MCP_ASSEMBLY");
        return string.IsNullOrWhiteSpace(packagedAssembly)
            ? Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")
            : Path.GetFullPath(packagedAssembly);
    }

    private static void AssertSchemaEnum(
        JsonElement? inputSchema,
        string propertyName,
        IReadOnlyList<string> expected)
    {
        var schema = JsonSerializer.SerializeToNode(inputSchema)!.AsObject();
        var values = schema["properties"]![propertyName]!["enum"]!.AsArray()
            .Where(static value => value is not null)
            .Select(static value => value!.GetValue<string>())
            .ToArray();
        Assert.Equal(expected, values);
    }
}
