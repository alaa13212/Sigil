using Sigil.Application.Services;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Tests.Services;

public class NormalizedMessageEnricherTests
{
    private readonly IMessageNormalizer _normalizer;
    private readonly NormalizedMessageEnricher _enricher;

    public NormalizedMessageEnricherTests()
    {
        _normalizer = Substitute.For<IMessageNormalizer>();
        _enricher = new NormalizedMessageEnricher(_normalizer);
    }

    private static ParsedEvent MakeEvent(string? message = null) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = Severity.Error,
        RawJson = "{}",
        Message = message,
    };

    private static EventParsingContext MakeContext(params TextNormalizationRule[] rules) => new()
    {
        ProjectId = 1,
        NormalizationRules = [..rules],
        AutoTagRules = [],
        InboundFilters = [],
        StackTraceFilters = [],
    };

    [Fact]
    public void Enrich_NullMessage_NormalizerNotCalled()
    {
        var evt = MakeEvent(message: null);
        _enricher.Enrich(evt, MakeContext());

        _normalizer.DidNotReceive().NormalizeMessage(
            Arg.Any<IReadOnlyCollection<TextNormalizationRule>>(), Arg.Any<string>());
    }

    [Fact]
    public void Enrich_EmptyMessage_NormalizerNotCalled()
    {
        var evt = MakeEvent(message: "");
        _enricher.Enrich(evt, MakeContext());

        _normalizer.DidNotReceive().NormalizeMessage(
            Arg.Any<IReadOnlyCollection<TextNormalizationRule>>(), Arg.Any<string>());
    }

    [Fact]
    public void Enrich_MessagePresent_NormalizerCalledAndResultStored()
    {
        _normalizer.NormalizeMessage(Arg.Any<IReadOnlyCollection<TextNormalizationRule>>(), "Error 42")
            .Returns("Error <NUM>");
        var evt = MakeEvent(message: "Error 42");

        _enricher.Enrich(evt, MakeContext());

        evt.NormalizedMessage.Should().Be("Error <NUM>");
        _normalizer.Received(1).NormalizeMessage(
            Arg.Any<IReadOnlyCollection<TextNormalizationRule>>(), "Error 42");
    }
}
