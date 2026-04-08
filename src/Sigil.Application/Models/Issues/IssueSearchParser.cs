namespace Sigil.Application.Models.Issues;

/// <summary>
/// Parses and serializes the issue search query string.
/// Format: free text tokens and tag filters in the form key:value or key:"value with spaces".
/// </summary>
public static class IssueSearchParser
{
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
}
