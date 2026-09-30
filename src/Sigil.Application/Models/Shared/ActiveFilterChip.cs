using Sigil.Application.Models.Issues;

namespace Sigil.Application.Models.Shared;

/// <summary>Which filter a chip clears when it is dismissed.</summary>
public enum ActiveFilterKind
{
    Status,
    Level,
    Sort,
    Bookmarked,
    DateRange,
    SearchToken,
}

/// <summary>One active filter: the label to render and the filter it clears when dismissed.</summary>
/// <param name="Kind">Filter the chip clears.</param>
/// <param name="Label">Text shown on the chip.</param>
/// <param name="Key">Key of the <c>key:value</c> search token, for <see cref="ActiveFilterKind.SearchToken"/>.</param>
/// <param name="Value">Value of the <c>key:value</c> search token, for <see cref="ActiveFilterKind.SearchToken"/>.</param>
public sealed record ActiveFilterChip(ActiveFilterKind Kind, string Label, string? Key = null, string? Value = null)
{
    /// <summary>Chip for one <c>key:value</c> token of a search query.</summary>
    public static ActiveFilterChip ForToken(string key, string value) =>
        new(ActiveFilterKind.SearchToken, $"{key}:{value}", key, value);
}

/// <summary>Builds the chips for the <c>key:value</c> tokens of a search query.</summary>
public static class SearchTokenChips
{
    /// <summary>One chip per token of <paramref name="search"/>, in the order the tokens appear.</summary>
    public static IReadOnlyList<ActiveFilterChip> FromSearch(string? search) =>
        [.. IssueSearchParser.Parse(search).TagFilters
            .Select(token => ActiveFilterChip.ForToken(token.Key, token.Value))];
}
