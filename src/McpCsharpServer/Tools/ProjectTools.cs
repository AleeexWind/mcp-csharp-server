using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using McpCsharpServer.Services;
using ModelContextProtocol.Server;

namespace McpCsharpServer.Tools;

[McpServerToolType]
public sealed class ProjectTools(ProjectSandbox sandbox, ToolInvocationLogger invocationLogger)
{
    private static readonly HashSet<string> SearchExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".md", ".json", ".csproj", ".sln", ".txt", ".yml", ".yaml", ".xml"
    };

    private static readonly string[] SkipDirNames =
    [
        ".git", "bin", "obj", "node_modules", ".vs", ".idea"
    ];

    [McpServerTool(Name = "search_project", UseStructuredContent = true),
     Description(
         "Searches text inside the project sandbox for a query string. " +
         "Only files under MCP_PROJECT_ROOT are readable; bin/obj/.git are skipped.")]
    public SearchProjectResult SearchProject(
        [Description("Text or symbol to find (case-insensitive substring).")] string query,
        [Description("Optional glob-like extension filter, e.g. *.cs. Empty = default code/docs set.")]
        string? filePattern = null,
        [Description("Maximum matches to return (1–50). Default: 20.")] int maxResults = 20)
    {
        const string tool = "search_project";
        invocationLogger.LogStart(tool, new { query, filePattern, maxResults });

        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("query is required.");
            }

            maxResults = Math.Clamp(maxResults, 1, 50);
            var matches = new List<SearchMatch>();

            foreach (var file in EnumerateSearchableFiles(filePattern))
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(file);
                }
                catch
                {
                    continue;
                }

                for (var i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    matches.Add(new SearchMatch(
                        Path: sandbox.ToRelative(file),
                        Line: i + 1,
                        Text: Truncate(lines[i].Trim(), 200)));

                    if (matches.Count >= maxResults)
                    {
                        break;
                    }
                }

                if (matches.Count >= maxResults)
                {
                    break;
                }
            }

            var result = new SearchProjectResult(
                Status: "success",
                Query: query,
                SandboxRoot: sandbox.Root,
                MatchCount: matches.Count,
                Matches: matches);

            invocationLogger.LogSuccess(tool, $"matches={matches.Count}");
            return result;
        }
        catch (Exception ex)
        {
            invocationLogger.LogError(tool, ex.Message);
            return new SearchProjectResult(
                Status: "error",
                Query: query,
                SandboxRoot: sandbox.Root,
                MatchCount: 0,
                Matches: [],
                Error: ex.Message);
        }
    }

    [McpServerTool(Name = "list_public_api", UseStructuredContent = true),
     Description(
         "Lists public types and public methods found in local .cs sources for a given type name " +
         "(substring match). Scoped to the project sandbox only.")]
    public ListPublicApiResult ListPublicApi(
        [Description("Type name or substring, e.g. ProjectSandbox or DocsTools.")] string typeName,
        [Description("Maximum types to return (1–30). Default: 10.")] int maxTypes = 10)
    {
        const string tool = "list_public_api";
        invocationLogger.LogStart(tool, new { typeName, maxTypes });

        try
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentException("typeName is required.");
            }

            maxTypes = Math.Clamp(maxTypes, 1, 30);
            var typeRegex = new Regex(
                @"\bpublic\s+(?:static\s+)?(?:sealed\s+)?(?:partial\s+)?(?:class|record|interface|struct)\s+(\w+)",
                RegexOptions.Compiled);
            var methodRegex = new Regex(
                @"\bpublic\s+(?:static\s+)?(?:async\s+)?[\w<>,\[\]\?]+\s+(\w+)\s*\(",
                RegexOptions.Compiled);

            var types = new List<PublicTypeInfo>();

            foreach (var file in EnumerateSearchableFiles("*.cs"))
            {
                var text = File.ReadAllText(file);
                var typeMatches = typeRegex.Matches(text);
                foreach (Match tm in typeMatches)
                {
                    var name = tm.Groups[1].Value;
                    if (!name.Contains(typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var start = tm.Index;
                    var next = typeMatches.Cast<Match>().SkipWhile(m => m.Index <= start).FirstOrDefault();
                    var slice = next is null ? text[start..] : text[start..next.Index];

                    var methods = methodRegex.Matches(slice)
                        .Select(m => m.Groups[1].Value)
                        .Where(m => m is not ("if" or "for" or "while" or "switch" or "using" or "class" or "record"))
                        .Distinct(StringComparer.Ordinal)
                        .Take(40)
                        .ToList();

                    types.Add(new PublicTypeInfo(
                        TypeName: name,
                        Path: sandbox.ToRelative(file),
                        PublicMethods: methods));

                    if (types.Count >= maxTypes)
                    {
                        break;
                    }
                }

                if (types.Count >= maxTypes)
                {
                    break;
                }
            }

            var result = new ListPublicApiResult(
                Status: "success",
                TypeQuery: typeName,
                TypeCount: types.Count,
                Types: types);

            invocationLogger.LogSuccess(tool, $"types={types.Count}");
            return result;
        }
        catch (Exception ex)
        {
            invocationLogger.LogError(tool, ex.Message);
            return new ListPublicApiResult(
                Status: "error",
                TypeQuery: typeName,
                TypeCount: 0,
                Types: [],
                Error: ex.Message);
        }
    }

    private IEnumerable<string> EnumerateSearchableFiles(string? filePattern)
    {
        var root = sandbox.Root;
        var extensionFilter = ParseExtensionFilter(filePattern);

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (!sandbox.IsUnderRoot(file))
            {
                continue;
            }

            var relative = sandbox.ToRelative(file);
            if (SkipDirNames.Any(skip =>
                    relative.StartsWith(skip + "/", StringComparison.OrdinalIgnoreCase)
                    || relative.Contains("/" + skip + "/", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var ext = Path.GetExtension(file);
            if (extensionFilter is not null)
            {
                if (!extensionFilter.Equals(ext, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            else if (!SearchExtensions.Contains(ext))
            {
                continue;
            }

            yield return file;
        }
    }

    private static string? ParseExtensionFilter(string? filePattern)
    {
        if (string.IsNullOrWhiteSpace(filePattern))
        {
            return null;
        }

        var p = filePattern.Trim();
        if (p.StartsWith("*.", StringComparison.Ordinal))
        {
            return p[1..];
        }

        if (p.StartsWith('.'))
        {
            return p;
        }

        return "." + p.TrimStart('*');
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}

public sealed record SearchProjectResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("sandboxRoot")] string SandboxRoot,
    [property: JsonPropertyName("matchCount")] int MatchCount,
    [property: JsonPropertyName("matches")] IReadOnlyList<SearchMatch> Matches,
    [property: JsonPropertyName("error")] string? Error = null);

public sealed record SearchMatch(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("text")] string Text);

public sealed record ListPublicApiResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("typeQuery")] string TypeQuery,
    [property: JsonPropertyName("typeCount")] int TypeCount,
    [property: JsonPropertyName("types")] IReadOnlyList<PublicTypeInfo> Types,
    [property: JsonPropertyName("error")] string? Error = null);

public sealed record PublicTypeInfo(
    [property: JsonPropertyName("typeName")] string TypeName,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("publicMethods")] IReadOnlyList<string> PublicMethods);
