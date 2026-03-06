using Sigil.Application.Services;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Tests.Services;

public class EventRankerTests
{
    private readonly EventRanker _ranker = new();

    private static ParsedEvent MakeEvent(DateTime timestamp) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        Timestamp = timestamp,
        Platform = Platform.CSharp,
        Level = Severity.Error,
        RawJson = "{}",
    };

    [Fact]
    public void GetMostRelevantEvent_SingleEvent_ReturnsIt()
    {
        var evt = MakeEvent(DateTime.UtcNow);

        _ranker.GetMostRelevantEvent([evt]).Should().BeSameAs(evt);
    }

    [Fact]
    public void GetMostRelevantEvent_MultipleEvents_ReturnsMostRecent()
    {
        var old = MakeEvent(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var recent = MakeEvent(new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var middle = MakeEvent(new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        _ranker.GetMostRelevantEvent([old, recent, middle]).Should().BeSameAs(recent);
    }

    [Fact]
    public void GetMostRelevantEvent_EqualTimestamps_ReturnsOne()
    {
        var ts = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var a = MakeEvent(ts);
        var b = MakeEvent(ts);

        var result = _ranker.GetMostRelevantEvent([a, b]);

        result.Should().Match<ParsedEvent>(e => e == a || e == b);
    }

    [Fact]
    public void GetMostRelevantEvent_EmptyCollection_ReturnsNull()
    {
        // MaxBy returns null for empty sequences; the ! suppresses the warning
        // but the result is actually null — callers must guard against this
        var result = _ranker.GetMostRelevantEvent(Array.Empty<ParsedEvent>());

        result.Should().BeNull();
    }
}
