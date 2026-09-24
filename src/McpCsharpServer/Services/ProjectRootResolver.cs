namespace McpCsharpServer.Services;

/// <summary>
/// Resolves the sandboxed project root used by file/search/command tools.
/// Priority: MCP_PROJECT_ROOT env → walk up from CWD looking for .git / docs → CWD.
/// </summary>
public static class ProjectRootResolver
{
    public static string Resolve()
    {
        var fromEnv = Environment.GetEnvironmentVariable("MCP_PROJECT_ROOT");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Path.GetFullPath(fromEnv.Trim());
        }

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var hasGit = Directory.Exists(Path.Combine(dir.FullName, ".git"));
            var hasDocs = Directory.Exists(Path.Combine(dir.FullName, "docs"));
            var hasCursor = Directory.Exists(Path.Combine(dir.FullName, ".cursor"));
            if (hasGit || (hasDocs && hasCursor))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Path.GetFullPath(Directory.GetCurrentDirectory());
    }
}
