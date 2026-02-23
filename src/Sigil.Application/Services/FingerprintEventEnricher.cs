using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class FingerprintEventEnricher(IFingerprintGenerator fingerprintGenerator) : IEventEnricher
{
    public Task Enrich(ParsedEvent parsedEvent, int projectId)
    {
        parsedEvent.Fingerprint = fingerprintGenerator.GenerateFingerprint(parsedEvent);
        return Task.CompletedTask;
    }
}