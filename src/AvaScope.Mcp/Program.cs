using System.Reflection;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddSingleton<LocalBridgeClient>();
builder.Services.AddSingleton<PreviewHostClient>();
builder.Services.AddSingleton(PreviewSessionStore.CreateDefault());
builder.Services.AddSingleton(static services => new PreviewSessionRegistry(
    services.GetRequiredService<SessionRegistry>(),
    services.GetRequiredService<PreviewHostClient>(),
    TimeProvider.System,
    services.GetRequiredService<PreviewSessionStore>()));
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<AvaScopeMcpTools>()
    .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, cancellationToken) =>
    {
        // Validate pure protocol DTOs before SDK argument binding can hide and log their exceptions.
        // Do not catch invocation failures here: application input may already have been dispatched.
        if (context.MatchedPrimitive is McpServerTool tool && tool.Metadata.OfType<MethodInfo>().FirstOrDefault() is { } method)
        {
            foreach (var parameter in method.GetParameters().Where(parameter => parameter.ParameterType.Assembly == typeof(RuntimeTextEditRequest).Assembly))
            {
                try
                {
                    if (context.Params?.Arguments?.TryGetValue(parameter.Name!, out var json) != true || json.ValueKind == JsonValueKind.Null)
                    {
                        if (parameter.HasDefaultValue) continue;
                        throw new ArgumentException("A required protocol argument is missing.");
                    }
                    JsonSerializer.Deserialize(json, parameter.ParameterType, McpJsonUtilities.DefaultOptions);
                }
                catch (Exception exception) when (exception is ArgumentException or JsonException)
                {
                    // Allow only this exact static explanation; constructor/converter messages can
                    // contain sensitive caller values and reflection may replace their TargetSite.
                    const string insertOffsets = "Insert requires a start offset within 0..8192 and no end offset.";
                    var message = parameter.ParameterType == typeof(RuntimeTextEditRequest) && exception.Message == insertOffsets
                        ? insertOffsets : "A protocol argument is missing or violates its published schema or domain constraints.";
                    var result = ToolResult<object>.Fail(new ProtocolError("invalid_mcp_arguments", message,
                        new Dictionary<string, string>
                        {
                            ["argument"] = parameter.Name!, ["stage"] = "request_validation", ["dispatched"] = "false",
                            ["nextAction"] = "Correct this argument using the tool's published schema and operation requirements, then submit the corrected request."
                        }));
                    var content = JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions);
                    return new CallToolResult
                    {
                        IsError = true, StructuredContent = content,
                        Content = [new TextContentBlock { Text = content.GetRawText() }]
                    };
                }
            }
        }
        return await next(context, cancellationToken);
    }));

builder.Services.PostConfigure<McpServerOptions>(options =>
{
    options.ServerInfo = new Implementation
    {
        Name = AvaScopeProtocol.ServiceName,
        Title = "AvaScope",
        Version = AvaScopeProduct.Version
    };
});

await builder.Build().RunAsync();
