using System.Text.RegularExpressions;

namespace McpCsharpServer.Services;

public sealed class DocsIndex(ProjectSandbox sandbox)
{
    private readonly Lazy<IReadOnlyList<DocEntry>> _entries = new(() => Load(sandbox));

    public IReadOnlyList<DocEntry> Entries => _entries.Value;

    public IReadOnlyList<DocHit> Search(string query, int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var terms = Regex.Split(query.Trim(), @"\s+")
            .Where(t => t.Length > 1)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToArray();

        if (terms.Length == 0)
        {
            return [];
        }

        return Entries
            .Select(doc =>
            {
                var haystack = $"{doc.Title}\n{doc.Package}\n{doc.Body}".ToLowerInvariant();
                var score = terms.Sum(t =>
                {
                    if (!haystack.Contains(t, StringComparison.Ordinal))
                    {
                        return 0;
                    }

                    var boost = doc.Title.Contains(t, StringComparison.OrdinalIgnoreCase) ? 3 : 1;
                    return boost;
                });

                var excerpt = ExtractExcerpt(doc.Body, terms);
                return new DocHit(doc, score, excerpt);
            })
            .Where(h => h.Score > 0)
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Doc.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maxResults, 1, 20))
            .ToList();
    }

    private static IReadOnlyList<DocEntry> Load(ProjectSandbox sandbox)
    {
        var docsDir = sandbox.DocsDirectory;
        if (!Directory.Exists(docsDir))
        {
            return [];
        }

        var list = new List<DocEntry>();
        foreach (var file in Directory.EnumerateFiles(docsDir, "*.md", SearchOption.AllDirectories))
        {
            if (!sandbox.IsUnderRoot(file))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            var title = ExtractTitle(text) ?? Path.GetFileNameWithoutExtension(file);
            var package = ExtractFrontMatter(text, "package") ?? title;
            var body = StripFrontMatter(text);
            list.Add(new DocEntry(
                Title: title,
                Package: package,
                RelativePath: sandbox.ToRelative(file),
                Body: body));
        }

        return list;
    }

    private static string? ExtractTitle(string text)
    {
        var match = Regex.Match(text, @"^#\s+(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractFrontMatter(string text, string key)
    {
        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            return null;
        }

        var end = text.IndexOf("---", 3, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        var fm = text[3..end];
        var match = Regex.Match(fm, $@"^{Regex.Escape(key)}\s*:\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim().Trim('"') : null;
    }

    private static string StripFrontMatter(string text)
    {
        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            return text;
        }

        var end = text.IndexOf("---", 3, StringComparison.Ordinal);
        return end < 0 ? text : text[(end + 3)..].TrimStart();
    }

    private static string ExtractExcerpt(string body, string[] terms)
    {
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (terms.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase)) && line.Length > 20)
            {
                return line.Length <= 240 ? line : line[..240] + "…";
            }
        }

        var first = lines.FirstOrDefault(l => !l.StartsWith('#')) ?? body;
        return first.Length <= 240 ? first : first[..240] + "…";
    }
}

public sealed record DocEntry(string Title, string Package, string RelativePath, string Body);

public sealed record DocHit(DocEntry Doc, int Score, string Excerpt);
