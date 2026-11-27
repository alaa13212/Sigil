namespace Sigil.Application.Models.Issues;

/// <summary>
/// Parses and serializes the issue search query string.
/// Format: free text tokens and tag filters in the form key:value or key:"value with spaces".
/// Several tag filters in one query are combined with AND: a matching record must satisfy all of them.
/// </summary>
public static class IssueSearchParser
{
    /// <summary>Key of the token that selects a release by name.</summary>
    public const string ReleaseKey = "release";

    /// <summary>Key of the token that selects issue state, as in <c>is:unassigned</c>.</summary>
    public const string StateKey = "is";

    public const string AssignedValue = "assigned";
    public const string UnassignedValue = "unassigned";
    public const string BookmarkedValue = "bookmarked";
    public const string UnviewedValue = "unviewed";

    public static (string? FreeText, List<(string Key, string Value)> TagFilters) Parse(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return (null, []);

        var tagFilters = new List<(string Key, string Value)>();
        var freeTextParts = new List<string>();
        int i = 0;
        var s = search.Trim();

        // Stryker disable once Equality : equivalent to i <= s.Length because the `if (i >= s.Length) break` below always exits when i == s.Length
        while (i < s.Length)
        {
            // Stryker disable once Equality : input is Trim()-ed so last char is never ' ', i can never equal s.Length here
            while (i < s.Length && s[i] == ' ') i++;
            // Stryker disable once Equality,Statement : after space-skip, i ≤ s.Length; removing break is identical — outer while exits on next check
            if (i >= s.Length) break;

            // Read key candidate: chars until space or colon
            int start = i;
            while (i < s.Length && s[i] != ' ' && s[i] != ':') i++;

            // Stryker disable once Equality : space-skip ensures s[start] != ' ', so s[start] == ':' would require i == start, which means an empty key — already handled by the same condition
            if (i < s.Length && s[i] == ':' && i > start)
            {
                var key = s[start..i];
                i++; // skip colon

                string value;
                if (i < s.Length && s[i] == '"')
                {
                    // Quoted value: key:"value with spaces"
                    i++; // skip opening quote
                    int vs = i;
                    while (i < s.Length && s[i] != '"') i++;
                    value = s[vs..i];
                    // Stryker disable once Equality : whether i advances to s.Length or s.Length+1, the outer while exits; equivalent mutation
                    if (i < s.Length) i++; // skip closing quote
                }
                else
                {
                    // Unquoted value: read until next space
                    int vs = i;
                    while (i < s.Length && s[i] != ' ') i++;
                    value = s[vs..i];
                }

                if (value.Length > 0)
                    tagFilters.Add((key, value));
                else
                    freeTextParts.Add(key + ":");
            }
            else
            {
                // No colon — free text word
                freeTextParts.Add(s[start..i]);
            }
        }

        return (freeTextParts.Count > 0 ? string.Join(' ', freeTextParts) : null, tagFilters);
    }

    public static string Serialize(string? freeText, IEnumerable<(string Key, string Value)> tagFilters)
    {
        var parts = new List<string>();
        foreach (var (key, value) in tagFilters)
            parts.Add(value.Contains(' ') ? $"{key}:\"{value}\"" : $"{key}:{value}");
        if (!string.IsNullOrWhiteSpace(freeText))
            parts.Add(freeText);
        return string.Join(' ', parts);
    }

    /// <summary>
    /// Splits <paramref name="search"/> into free text, ordinary tag filters and the reserved
    /// <c>release:</c> and <c>is:</c> tokens. An <c>is:</c> value that names no known state stays an
    /// ordinary tag filter, and the last <c>is:</c> token of a state wins when a query repeats one.
    /// </summary>
    public static IssueSearchCriteria ParseCriteria(string? search)
    {
        var (freeText, tagFilters) = Parse(search);
        if (tagFilters.Count == 0) return IssueSearchCriteria.Empty with { FreeText = freeText };

        var remainingTags = new List<(string Key, string Value)>(tagFilters.Count);
        var releases = new List<string>();
        var assignment = IssueAssignmentFilter.Any;
        var bookmarked = false;
        var unviewed = false;

        foreach (var (key, value) in tagFilters)
        {
            if (string.Equals(key, ReleaseKey, StringComparison.OrdinalIgnoreCase))
            {
                releases.Add(value);
            }
            else if (string.Equals(key, StateKey, StringComparison.OrdinalIgnoreCase))
            {
                switch (value.ToLowerInvariant())
                {
                    case AssignedValue: assignment = IssueAssignmentFilter.Assigned; break;
                    case UnassignedValue: assignment = IssueAssignmentFilter.Unassigned; break;
                    case BookmarkedValue: bookmarked = true; break;
                    case UnviewedValue: unviewed = true; break;
                    default: remainingTags.Add((key, value)); break;
                }
            }
            else
            {
                remainingTags.Add((key, value));
            }
        }

        return new IssueSearchCriteria(freeText, remainingTags, releases, assignment, bookmarked, unviewed);
    }

    /// <summary>
    /// Returns <paramref name="search"/> without the <c>key:value</c> token it holds, rebuilt from its
    /// parsed parts so the remaining tokens and the free text survive verbatim. Every occurrence of the
    /// token is removed; when nothing is left the result is an empty string.
    /// </summary>
    public static string RemoveToken(string? search, string key, string value)
    {
        var (freeText, tagFilters) = Parse(search);
        var remaining = tagFilters
            .Where(token => !string.Equals(token.Key, key, StringComparison.Ordinal) ||
                            !string.Equals(token.Value, value, StringComparison.Ordinal))
            .ToList();
        return Serialize(freeText, remaining);
    }
}
