using System.ComponentModel;
using System.Text.Json.Serialization;
using McpCsharpServer.Services;
using ModelContextProtocol.Server;

namespace McpCsharpServer.Tools;

[McpServerToolType]
public sealed class DocsTools(DocsIndex docsIndex, ToolInvocationLogger invocationLogger)
{
    [McpServerTool(Name = "lookup_docs", UseStructuredContent = true),
     Description(
         "Looks up local documentation (markdown under docs/) by package, class, or topic. " +
         "Returns structured excerpts and relative file paths — Context7-style local context.")]
    public LookupDocsResult LookupDocs(
        [Description("Search query: package name, type name, or topic keywords.")] string query,
        [Description("Maximum number of hits to return (1–20). Default: 5.")] int maxResults = 5)
    {
        const string tool = "lookup_docs";
        invocationLogger.LogStart(tool, new { query, maxResults });

        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("query is required.");
            }

            var hits = docsIndex.Search(query, maxResults);
            var result = new LookupDocsResult(
                Status: "success",
                Query: query,
                TotalIndexed: docsIndex.Entries.Count,
                Results: hits.Select(h => new DocResultItem(
                    Title: h.Doc.Title,
                    Package: h.Doc.Package,
                    Path: h.Doc.RelativePath,
                    Score: h.Score,
                    Excerpt: h.Excerpt,
                    Link: $"file://{h.Doc.RelativePath}"
                )).ToList());

            invocationLogger.LogSuccess(tool, $"hits={result.Results.Count}");
            return result;
        }
        catch (Exception ex)
        {
            invocationLogger.LogError(tool, ex.Message);
            return new LookupDocsResult(
                Status: "error",
                Query: query,
                TotalIndexed: docsIndex.Entries.Count,
                Results: [],
                Error: ex.Message);
        }
    }
}

public sealed record LookupDocsResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("totalIndexed")] int TotalIndexed,
    [property: JsonPropertyName("results")] IReadOnlyList<DocResultItem> Results,
    [property: JsonPropertyName("error")] string? Error = null);

public sealed record DocResultItem(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("package")] string Package,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("excerpt")] string Excerpt,
    [property: JsonPropertyName("link")] string Link);
