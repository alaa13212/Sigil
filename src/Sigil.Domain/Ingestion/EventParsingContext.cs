using Sigil.Domain.Entities;

namespace Sigil.Domain.Ingestion;

public class EventParsingContext
{
    public required int ProjectId { get; init; }
    public required List<TextNormalizationRule> NormalizationRules { get; init; }
    public required List<AutoTagRule> AutoTagRules { get; init; }
    public required List<EventFilter> InboundFilters { get; init; }
    public int HighVolumeThreshold { get; init; } = 1000;
}
