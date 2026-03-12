using Sigil.Application.Models.SourceCode;

namespace Sigil.Infrastructure.Integrations;

internal static class SourceCodeClientHelper
{
    /// <summary>
    /// Strips leading slashes and normalizes backslashes to forward slashes.
    /// </summary>
    internal static string NormalizePath(string filePath) =>
        filePath.TrimStart('/', '\\').Replace('\\', '/');

    /// <summary>
    /// Finds the best path match in a repository tree for a Sentry-provided file path,
    /// which may be a bare filename, sub-project relative path, or full repo-root path.
    /// Prefers exact suffix match, then filename-only match (shortest path wins).
    /// </summary>
    internal static string? FindBestMatch(IEnumerable<string> treePaths, string candidate)
    {
        var fileName = Path.GetFileName(candidate).ToLowerInvariant();
        var candidateLower = candidate.ToLowerInvariant().Replace('\\', '/');

        var suffixMatch = treePaths.FirstOrDefault(p =>
            p.ToLowerInvariant().EndsWith("/" + candidateLower) ||
            string.Equals(p, candidate, StringComparison.OrdinalIgnoreCase));

        if (suffixMatch != null) return suffixMatch;

        var nameMatches = treePaths
            .Where(p => string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Prefer in-app heuristic: shorter paths are usually the app source rather than deps
        return nameMatches.MinBy(p => p.Length);
    }

    /// <summary>
    /// Extracts a window of lines around <paramref name="lineNumber"/> from a raw file string.
    /// </summary>
    internal static List<SourceLine> ExtractContext(string fileContent, int lineNumber, int contextLines)
    {
        var lines = fileContent.Split('\n');
        var start = Math.Max(0, lineNumber - contextLines - 1);
        var end = Math.Min(lines.Length - 1, lineNumber + contextLines - 1);

        return Enumerable.Range(start, end - start + 1)
            .Select(i => new SourceLine(i + 1, lines[i]))
            .ToList();
    }
}
