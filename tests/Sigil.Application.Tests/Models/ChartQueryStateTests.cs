using Sigil.Application.Models.Common;
using Sigil.Application.Models.Shared;

namespace Sigil.Application.Tests.Models;

public class ChartQueryStateTests
{
    private static Uri Apply(string uri, TimeSeriesChartView view) =>
        ChartQueryState.Apply(new Uri(uri), view);

    [Fact]
    public void Apply_WritesEveryPartOfTheView()
    {
        var result = Apply("https://sigil.test/projects/1/issues/2", new TimeSeriesChartView(30, TimeSeriesGranularity.Day, TimeSeriesChartMode.Line));

        result.Query.Should().Be("?range=30&buckets=Day&chart=Line");
    }

    [Fact]
    public void Apply_KeepsParametersTheChartDoesNotOwn()
    {
        var result = Apply("https://sigil.test/projects/1/issues/2?tab=SuggestedEvent&event=42",
            new TimeSeriesChartView(7, TimeSeriesGranularity.SixHour, TimeSeriesChartMode.Bar));

        result.Query.Should().Contain("tab=SuggestedEvent");
        result.Query.Should().Contain("event=42");
        result.Query.Should().Contain("range=7");
        result.Query.Should().Contain("buckets=SixHour");
    }

    [Fact]
    public void Apply_LeavesTheDefaultSelectionOutOfTheUrl()
    {
        var result = Apply("https://sigil.test/projects/1/issues/2",
            new TimeSeriesChartView(14, TimeSeriesGranularity.Auto, TimeSeriesChartMode.Bar));

        result.Query.Should().Be("?range=14");
    }

    [Fact]
    public void Apply_RemovesAPreviousOverrideWhenTheSelectionReturnsToTheDefault()
    {
        var view = new TimeSeriesChartView(14, TimeSeriesGranularity.Auto, TimeSeriesChartMode.Bar);

        var result = Apply("https://sigil.test/projects/1/issues/2?range=30&buckets=Week&chart=Line", view);

        result.Query.Should().Be("?range=14");
    }

    [Fact]
    public void Apply_ReplacesAnExistingSelectionRatherThanAppending()
    {
        var result = Apply("https://sigil.test/projects/1/issues/2?range=7&buckets=Hour",
            new TimeSeriesChartView(30, TimeSeriesGranularity.Week, TimeSeriesChartMode.Line));

        result.Query.Should().Be("?range=30&buckets=Week&chart=Line");
    }

    [Fact]
    public void Apply_KeepsThePathAndTheFragment()
    {
        var result = Apply("https://sigil.test/projects/1/releases/2#top",
            new TimeSeriesChartView(1, TimeSeriesGranularity.Hour, TimeSeriesChartMode.Bar));

        result.AbsolutePath.Should().Be("/projects/1/releases/2");
        result.Fragment.Should().Be("#top");
    }

    [Fact]
    public void Apply_WithoutAQueryString_ProducesASingleQuestionMark()
    {
        var result = Apply("https://sigil.test/projects/1/issues/2", new TimeSeriesChartView(1, TimeSeriesGranularity.Auto, TimeSeriesChartMode.Bar));

        result.Query.Should().Be("?range=1");
        result.Query.Should().NotContain("??");
    }
}
