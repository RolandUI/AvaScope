using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    .WithTools<AvaScopeMcpTools>();

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
