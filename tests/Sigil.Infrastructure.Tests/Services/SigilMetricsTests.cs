using Sigil.Application.Interfaces;
using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class SigilMetricsTests
{
    private static readonly DateTime T = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NewInstance_StartsAtZero()
    {
        var metrics = new SigilMetrics();

        var snapshot = metrics.GetSnapshot();
        snapshot.EventsIngested.Should().Be(0);
        snapshot.EventsDropped.Should().Be(0);
        snapshot.LastIngestionUtc.Should().BeNull();
    }

    [Fact]
    public void RecordEventsIngested_Accumulates()
    {
        var metrics = new SigilMetrics();

        metrics.RecordEventsIngested(3);
        metrics.RecordEventsIngested(4);

        metrics.GetSnapshot().EventsIngested.Should().Be(7);
    }

    [Fact]
    public void RecordEventsDropped_Accumulates()
    {
        var metrics = new SigilMetrics();

        metrics.RecordEventsDropped(2);
        metrics.RecordEventsDropped(5);

        metrics.GetSnapshot().EventsDropped.Should().Be(7);
    }

    [Fact]
    public void RecordEvents_IgnoresNonPositiveCounts()
    {
        var metrics = new SigilMetrics();

        metrics.RecordEventsIngested(0);
        metrics.RecordEventsIngested(-5);
        metrics.RecordEventsDropped(0);
        metrics.RecordEventsDropped(-1);

        var snapshot = metrics.GetSnapshot();
        snapshot.EventsIngested.Should().Be(0);
        snapshot.EventsDropped.Should().Be(0);
    }

    [Fact]
    public void RecordLastIngestion_StoresTheUtcInstant()
    {
        var metrics = new SigilMetrics();

        metrics.RecordLastIngestion(T);

        metrics.GetSnapshot().LastIngestionUtc.Should().Be(T);
    }

    [Fact]
    public void RecordLastIngestion_KeepsTheMostRecentInstant()
    {
        var metrics = new SigilMetrics();

        metrics.RecordLastIngestion(T);
        metrics.RecordLastIngestion(T.AddMinutes(-5));

        metrics.GetSnapshot().LastIngestionUtc.Should().Be(T);
    }

    [Fact]
    public void RecordLastIngestion_AdvancesToALaterInstant()
    {
        var metrics = new SigilMetrics();

        metrics.RecordLastIngestion(T);
        metrics.RecordLastIngestion(T.AddMinutes(5));

        metrics.GetSnapshot().LastIngestionUtc.Should().Be(T.AddMinutes(5));
    }

    [Fact]
    public void RecordLastIngestion_NormalisesLocalInstantsToUtc()
    {
        var metrics = new SigilMetrics();

        metrics.RecordLastIngestion(T.ToLocalTime());

        metrics.GetSnapshot().LastIngestionUtc.Should().Be(T);
    }

    [Fact]
    public void GetSnapshot_ReportsUtcKind()
    {
        var metrics = new SigilMetrics();
        metrics.RecordLastIngestion(T.ToLocalTime());

        metrics.GetSnapshot().LastIngestionUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ConcurrentEmits_AreNotLost()
    {
        var metrics = new SigilMetrics();

        Parallel.For(0, 500, _ =>
        {
            metrics.RecordEventsIngested(1);
            metrics.RecordEventsDropped(1);
        });

        var snapshot = metrics.GetSnapshot();
        snapshot.EventsIngested.Should().Be(500);
        snapshot.EventsDropped.Should().Be(500);
    }

    [Fact]
    public void ConcurrentLastIngestionEmits_ConvergeOnTheLatest()
    {
        var metrics = new SigilMetrics();

        Parallel.For(0, 500, i => metrics.RecordLastIngestion(T.AddSeconds(i)));

        metrics.GetSnapshot().LastIngestionUtc.Should().Be(T.AddSeconds(499));
    }

    [Fact]
    public void ImplementsTheApplicationInterface()
    {
        new SigilMetrics().Should().BeAssignableTo<ISigilMetrics>();
    }
}
