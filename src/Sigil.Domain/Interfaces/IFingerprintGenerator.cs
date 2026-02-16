using Sigil.Domain.Ingestion;

namespace Sigil.Domain.Interfaces;

public interface IFingerprintGenerator
{
    string GenerateFingerprint(ParsedEvent parsedEvent);
}