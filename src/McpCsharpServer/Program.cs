using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using McpCsharpServer.Services;
using McpCsharpServer.Tools;

var builder = Host.CreateApplicationBuilder(args);

// MCP uses stdout for protocol messages — all logs must go to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

var projectRoot = ProjectRootResolver.Resolve();
builder.Services.AddSingleton(new ProjectSandbox(projectRoot));
builder.Services.AddSingleton<DocsIndex>();
builder.Services.AddSingleton<ToolInvocationLogger>();
builder.Services.AddSingleton<DocsTools>();
builder.Services.AddSingleton<ProjectTools>();
builder.Services.AddSingleton<DevOpsTools>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation("MCP server starting. ProjectRoot={ProjectRoot}", projectRoot);

await host.RunAsync();
