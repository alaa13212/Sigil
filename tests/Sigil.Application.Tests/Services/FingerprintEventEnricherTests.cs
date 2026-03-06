using Sigil.Application.Services;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Tests.Services;

public class FingerprintEventEnricherTests
{
    private readonly IFingerprintGenerator _generator;
    private readonly FingerprintEventEnricher _enricher;

    public FingerprintEventEnricherTests()
    {
        _generator = Substitute.For<IFingerprintGenerator>();
        _enricher = new FingerprintEventEnricher(_generator);
    }

    private static ParsedEvent MakeEvent() => new()
    {
        EventId = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = Severity.Error,
        RawJson = "{}",
    };

    private static EventParsingContext MakeContext() => new()
    {
        ProjectId = 1,
        NormalizationRules = [],
        AutoTagRules = [],
        InboundFilters = [],
        StackTraceFilters = [],
    };

    [Fact]
    public void Enrich_DelegatesToGenerator_StoresResult()
    {
        _generator.GenerateFingerprint(Arg.Any<ParsedEvent>()).Returns("fp-abc123");
        var evt = MakeEvent();

        _enricher.Enrich(evt, MakeContext());

        evt.Fingerprint.Should().Be("fp-abc123");
        _generator.Received(1).GenerateFingerprint(evt);
    }

    [Fact]
    public void Enrich_OverwritesExistingFingerprint()
    {
        _generator.GenerateFingerprint(Arg.Any<ParsedEvent>()).Returns("fp-new");
        var evt = MakeEvent();
        evt.Fingerprint = "fp-old";

        _enricher.Enrich(evt, MakeContext());

        evt.Fingerprint.Should().Be("fp-new");
    }

    [Fact]
    public void Enrich_GeneratorReturnsNull_FingerprintSetToNull()
    {
        _generator.GenerateFingerprint(Arg.Any<ParsedEvent>()).Returns((string?)null);
        var evt = MakeEvent();
        evt.Fingerprint = "fp-existing";

        _enricher.Enrich(evt, MakeContext());

        evt.Fingerprint.Should().BeNull();
    }
}
