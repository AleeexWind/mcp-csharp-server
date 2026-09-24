namespace McpCsharpServer.Services;

/// <summary>
/// Restricts file system access to <see cref="Root"/> and its descendants.
/// </summary>
public sealed class ProjectSandbox(string root)
{
    public string Root { get; } = Path.GetFullPath(root);

    public string DocsDirectory => Path.Combine(Root, "docs");

    public string ResolveSafePath(string relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
        {
            throw new InvalidOperationException("Path must not be empty.");
        }

        var candidate = Path.IsPathRooted(relativeOrAbsolute)
            ? Path.GetFullPath(relativeOrAbsolute)
            : Path.GetFullPath(Path.Combine(Root, relativeOrAbsolute));

        if (!IsUnderRoot(candidate))
        {
            throw new UnauthorizedAccessException(
                $"Path is outside the project sandbox ({Root}): {candidate}");
        }

        return candidate;
    }

    public bool IsUnderRoot(string fullPath)
    {
        var normalized = Path.GetFullPath(fullPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalized.Equals(root, StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public string ToRelative(string fullPath)
    {
        var absolute = Path.GetFullPath(fullPath);
        return Path.GetRelativePath(Root, absolute).Replace('\\', '/');
    }
}
