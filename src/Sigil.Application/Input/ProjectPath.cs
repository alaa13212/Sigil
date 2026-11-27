using System.Text.RegularExpressions;

namespace Sigil.Application.Input;

/// <summary>Reads the project a page belongs to out of its path.</summary>
public static class ProjectPath
{
    private static readonly Regex ProjectIdRegex = new(@"^/projects/(\d+)(?:/|$)", RegexOptions.Compiled);

    /// <summary>Id of the project the path belongs to, or null when the path has none.</summary>
    public static int? ProjectIdFrom(string uriOrPath)
    {
        if (string.IsNullOrWhiteSpace(uriOrPath)) return null;

        var path = uriOrPath;
        if (Uri.TryCreate(uriOrPath, UriKind.Absolute, out var uri))
            path = uri.AbsolutePath;

        var match = ProjectIdRegex.Match(path);
        return match.Success && int.TryParse(match.Groups[1].Value, out var id) ? id : null;
    }
}
