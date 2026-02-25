using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class FingerprintEventEnricher(IFingerprintGenerator fingerprintGenerator) : IEventEnricher
{
    public void Enrich(ParsedEvent parsedEvent, EventParsingContext context)
    {
        parsedEvent.Fingerprint = fingerprintGenerator.GenerateFingerprint(parsedEvent);
    }
}