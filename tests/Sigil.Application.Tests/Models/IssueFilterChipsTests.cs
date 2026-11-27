using Sigil.Application.Models.Issues;
using Sigil.Application.Models.Shared;
using Sigil.Domain.Enums;

namespace Sigil.Application.Tests.Models;

public class IssueFilterChipsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private static IssueFilterState State(
        IssueStatus? status = null,
        Severity? level = null,
        IssueSortBy? sort = null,
        bool bookmarked = false,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        string? search = null) =>
        new(status, level, sort, bookmarked, since, until, search);

    [Fact]
    public void Describe_NoFilters_ProducesNoChips()
    {
        IssueFilterChips.Describe(State(), IssueStatus.Open, Now).Should().BeEmpty();
    }

    [Fact]
    public void Describe_DefaultStatus_ProducesNoChip()
    {
        IssueFilterChips.Describe(State(status: IssueStatus.Open), IssueStatus.Open, Now).Should().BeEmpty();
    }

    [Fact]
    public void Describe_StatusOtherThanTheDefault_ProducesOneChip()
    {
        var chips = IssueFilterChips.Describe(State(status: IssueStatus.Resolved), IssueStatus.Open, Now);

        chips.Should().ContainSingle().Which.Should().Be(
            new ActiveFilterChip(ActiveFilterKind.Status, "Resolved"));
    }

    [Fact]
    public void Describe_Level_ProducesOneChip()
    {
        var chips = IssueFilterChips.Describe(State(level: Severity.Error), IssueStatus.Open, Now);

        chips.Should().ContainSingle().Which.Should().Be(
            new ActiveFilterChip(ActiveFilterKind.Level, "Error"));
    }

    [Fact]
    public void Describe_DefaultSort_ProducesNoChip()
    {
        IssueFilterChips.Describe(State(sort: IssueSortBy.LastSeen), IssueStatus.Open, Now).Should().BeEmpty();
    }

    [Fact]
    public void Describe_OtherSort_ProducesOneChip()
    {
        var chips = IssueFilterChips.Describe(State(sort: IssueSortBy.OccurrenceCount), IssueStatus.Open, Now);

        chips.Should().ContainSingle().Which.Kind.Should().Be(ActiveFilterKind.Sort);
        chips[0].Label.Should().Be("Occurrences");
    }

    [Fact]
    public void Describe_Bookmarked_ProducesOneChip()
    {
        var chips = IssueFilterChips.Describe(State(bookmarked: true), IssueStatus.Open, Now);

        chips.Should().ContainSingle().Which.Kind.Should().Be(ActiveFilterKind.Bookmarked);
    }

    [Theory]
    [InlineData(24, "Last 24 hours")]
    [InlineData(24 * 7, "Last 7 days")]
    [InlineData(24 * 30, "Last 30 days")]
    [InlineData(24 * 90, "Last 90 days")]
    public void Describe_RangeEndingNow_IsNamedAfterItsPreset(int hours, string expected)
    {
        var chips = IssueFilterChips.Describe(
            State(since: Now.AddHours(-hours), until: Now), IssueStatus.Open, Now);

        chips.Should().ContainSingle().Which.Should().Be(new ActiveFilterChip(ActiveFilterKind.DateRange, expected));
    }

    [Fact]
    public void Describe_CustomRangeEndingNow_IsNamedByItsDates()
    {
        var from = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var chips = IssueFilterChips.Describe(State(since: from, until: Now), IssueStatus.Open, Now);

        chips.Should().ContainSingle().Which.Label.Should().Be("2026-03-01 – 2026-03-15");
    }

    [Fact]
    public void Describe_RangeInThePastWithAPresetLength_IsNamedByItsDates()
    {
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(30);

        var chips = IssueFilterChips.Describe(State(since: from, until: to), IssueStatus.Open, Now);

        chips.Should().ContainSingle().Which.Label.Should().Be("2026-01-01 – 2026-01-31");
    }

    [Fact]
    public void Describe_OpenLowerBound_ProducesOneChip()
    {
        var from = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var chips = IssueFilterChips.Describe(State(since: from), IssueStatus.Open, Now);

        chips.Should().ContainSingle().Which.Label.Should().Be("Since 2026-03-01");
    }

    [Fact]
    public void Describe_OpenUpperBound_ProducesOneChip()
    {
        var to = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

        var chips = IssueFilterChips.Describe(State(until: to), IssueStatus.Open, Now);

        chips.Should().ContainSingle().Which.Label.Should().Be("Until 2026-03-01");
    }

    [Fact]
    public void Describe_SearchTokens_ProduceOneChipEachInQueryOrder()
    {
        var chips = IssueFilterChips.Describe(
            State(search: "crash env:prod is:regression tag:env:prod release:1.0"),
            IssueStatus.Open, Now);

        chips.Should().HaveCount(4);
        chips.Select(c => c.Label).Should().BeEquivalentTo(
            ["env:prod", "is:regression", "tag:env:prod", "release:1.0"], options => options.WithStrictOrdering());
        chips.Should().OnlyContain(c => c.Kind == ActiveFilterKind.SearchToken);
    }

    [Fact]
    public void Describe_TokenChip_CarriesTheKeyAndValueToRemove()
    {
        var chips = IssueFilterChips.Describe(State(search: "env:prod"), IssueStatus.Open, Now);

        chips.Should().ContainSingle().Which.Key.Should().Be("env");
        chips[0].Value.Should().Be("prod");
    }

    [Fact]
    public void Describe_SearchFreeTextOnly_ProducesNoChips()
    {
        IssueFilterChips.Describe(State(search: "crash timeout"), IssueStatus.Open, Now).Should().BeEmpty();
    }

    [Fact]
    public void Describe_AllFilters_ProduceOneChipEach()
    {
        var chips = IssueFilterChips.Describe(
            State(
                status: IssueStatus.Resolved,
                level: Severity.Error,
                sort: IssueSortBy.Priority,
                bookmarked: true,
                since: Now.AddDays(-30),
                until: Now,
                search: "env:prod is:unassigned"),
            IssueStatus.Open, Now);

        chips.Select(c => c.Kind).Should().BeEquivalentTo(
        [
            ActiveFilterKind.Status,
            ActiveFilterKind.Level,
            ActiveFilterKind.DateRange,
            ActiveFilterKind.Bookmarked,
            ActiveFilterKind.Sort,
            ActiveFilterKind.SearchToken,
            ActiveFilterKind.SearchToken,
        ]);

        chips.Should().OnlyHaveUniqueItems(chip => $"{chip.Kind}|{chip.Key}|{chip.Value}");
    }
}
