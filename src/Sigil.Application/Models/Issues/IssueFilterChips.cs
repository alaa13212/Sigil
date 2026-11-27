using Sigil.Application.Models.Shared;
using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Issues;

/// <summary>Filter selections an issue listing is currently showing.</summary>
/// <param name="Status">Status to show, or <c>null</c> for every status.</param>
/// <param name="Level">Severity to show, or <c>null</c> for every severity.</param>
/// <param name="Sort">Sort selection, or <c>null</c> for the listing's default sort.</param>
/// <param name="Bookmarked">True when only bookmarked issues are shown.</param>
/// <param name="Since">Inclusive lower bound on <c>Issue.LastSeen</c>.</param>
/// <param name="Until">Inclusive upper bound on <c>Issue.LastSeen</c>.</param>
/// <param name="Search">Raw search query, holding free text and <c>key:value</c> tokens.</param>
public sealed record IssueFilterState(
    IssueStatus? Status,
    Severity? Level,
    IssueSortBy? Sort,
    bool Bookmarked,
    DateTimeOffset? Since,
    DateTimeOffset? Until,
    string? Search);

/// <summary>Builds the dismissible chips for the filters an issue listing is showing.</summary>
public static class IssueFilterChips
{
    private static readonly (TimeSpan Length, string Label)[] RangePresets =
    [
        (TimeSpan.FromHours(24), "Last 24 hours"),
        (TimeSpan.FromDays(7), "Last 7 days"),
        (TimeSpan.FromDays(30), "Last 30 days"),
        (TimeSpan.FromDays(90), "Last 90 days"),
    ];

    private static readonly TimeSpan PresetTolerance = TimeSpan.FromSeconds(1);

    /// <summary>
    /// One chip per filter the listing is showing, in display order. A selection equal to
    /// <paramref name="defaultStatus"/> or to the default sort is not a filter the user applied and gets
    /// no chip; the free text of <see cref="IssueFilterState.Search"/> is shown in the search box and gets
    /// no chip either.
    /// </summary>
    public static IReadOnlyList<ActiveFilterChip> Describe(IssueFilterState state, IssueStatus defaultStatus, DateTimeOffset now)
    {
        var chips = new List<ActiveFilterChip>();

        if (state.Status is { } status && status != defaultStatus)
            chips.Add(new ActiveFilterChip(ActiveFilterKind.Status, status.ToString()));

        if (state.Level is { } level)
            chips.Add(new ActiveFilterChip(ActiveFilterKind.Level, level.ToString()));

        if (DescribeRange(state.Since, state.Until, now) is { } range)
            chips.Add(new ActiveFilterChip(ActiveFilterKind.DateRange, range));

        if (state.Bookmarked)
            chips.Add(new ActiveFilterChip(ActiveFilterKind.Bookmarked, "Bookmarked"));

        if (state.Sort is { } sort && sort != IssueSortBy.LastSeen)
            chips.Add(new ActiveFilterChip(ActiveFilterKind.Sort, SortLabel(sort)));

        chips.AddRange(SearchTokenChips.FromSearch(state.Search));
        return chips;
    }

    /// <summary>
    /// Label for a date range, or <c>null</c> when the range is unbounded. A range that ends now and
    /// spans a preset length is named after that preset; any other bounded range is named by its dates.
    /// </summary>
    public static string? DescribeRange(DateTimeOffset? since, DateTimeOffset? until, DateTimeOffset now)
    {
        if (since is null && until is null) return null;

        if (since is { } from && until is { } to)
        {
            if ((to - now).Duration() <= PresetTolerance)
            {
                var length = to - from;
                foreach (var (presetLength, label) in RangePresets)
                    if ((length - presetLength).Duration() <= PresetTolerance)
                        return label;
            }

            return $"{UtcDateRangeInput.ToDateString(from)} – {UtcDateRangeInput.ToDateString(to)}";
        }

        return since is { } start
            ? $"Since {UtcDateRangeInput.ToDateString(start)}"
            : $"Until {UtcDateRangeInput.ToDateString(until!.Value)}";
    }

    private static string SortLabel(IssueSortBy sort) => sort switch
    {
        IssueSortBy.FirstSeen => "First seen",
        IssueSortBy.OccurrenceCount => "Occurrences",
        IssueSortBy.Priority => "Priority",
        _ => sort.ToString(),
    };
}
