using System.Text.Json;
using ModelContextProtocol.Client;

var repoRoot = FindRepoRoot();
var serverProject = Path.Combine(repoRoot, "src", "McpCsharpServer", "McpCsharpServer.csproj");
var evidenceDir = Path.Combine(repoRoot, "evidence");
Directory.CreateDirectory(evidenceDir);

Environment.SetEnvironmentVariable("MCP_PROJECT_ROOT", repoRoot);

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "mcp-csharp-server",
    Command = "dotnet",
    Arguments = ["run", "--project", serverProject, "--no-build"],
    WorkingDirectory = repoRoot,
    EnvironmentVariables = new Dictionary<string, string?>
    {
        ["MCP_PROJECT_ROOT"] = repoRoot
    }
});

await using var client = await McpClient.CreateAsync(transport);

var tools = await client.ListToolsAsync();
Console.WriteLine("Available tools:");
foreach (var t in tools)
{
    Console.WriteLine($" - {t.Name}: {t.Description}");
}

var calls = new (string Tool, Dictionary<string, object?> ToolArgs, string PromptHint)[]
{
    ("lookup_docs", new() { ["query"] = "ProjectSandbox", ["maxResults"] = 3 },
        "Найди локальную документацию по ProjectSandbox"),
    ("search_project", new() { ["query"] = "AddMcpServer", ["filePattern"] = "*.cs", ["maxResults"] = 10 },
        "Где в проекте вызывается AddMcpServer?"),
    ("list_public_api", new() { ["typeName"] = "DocsTools", ["maxTypes"] = 5 },
        "Покажи публичные методы DocsTools"),
    ("run_allowed_command", new() { ["commandKey"] = "list-allowed" },
        "Какие команды разрешены в run_allowed_command?"),
    ("run_allowed_command", new() { ["commandKey"] = "rm -rf /" },
        "Запусти rm -rf / через MCP (ожидаем отказ)")
};

var reportLines = new List<string>
{
    "# Smoke-test evidence (MCP tool calls)",
    "",
    $"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}",
    $"ProjectRoot: `{repoRoot}`",
    "",
    "These calls were made by `src/McpSmokeTest` against the stdio MCP server.",
    ""
};

var index = 1;
foreach (var (tool, toolArgs, prompt) in calls)
{
    Console.WriteLine($"\n=== Call {index}: {tool} ===");
    var result = await client.CallToolAsync(tool, toolArgs);
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(json);

    var fileName = $"call-{index:00}-{tool}.json";
    await File.WriteAllTextAsync(Path.Combine(evidenceDir, fileName), json);

    reportLines.Add($"## {index}. Prompt (пример для IDE)");
    reportLines.Add("");
    reportLines.Add($"> {prompt}");
    reportLines.Add("");
    reportLines.Add($"- **Expected tool:** `{tool}`");
    reportLines.Add($"- **Args:** `{JsonSerializer.Serialize(toolArgs)}`");
    reportLines.Add($"- **IsError:** `{result.IsError}`");
    reportLines.Add($"- **Evidence file:** `evidence/{fileName}`");
    reportLines.Add("");
    index++;
}

await File.WriteAllTextAsync(Path.Combine(evidenceDir, "README.md"), string.Join('\n', reportLines));
Console.WriteLine($"\nEvidence written to {evidenceDir}");

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, ".cursor", "mcp.json"))
            || Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            // Prefer repo root (has .cursor/mcp.json), not bin folders
            if (File.Exists(Path.Combine(dir.FullName, ".cursor", "mcp.json")))
            {
                return dir.FullName;
            }
        }

        dir = dir.Parent;
    }

    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
