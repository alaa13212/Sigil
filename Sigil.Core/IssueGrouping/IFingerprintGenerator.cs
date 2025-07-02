using Sigil.Core.Parsing.Models;

namespace Sigil.Core.IssueGrouping;

public interface IFingerprintGenerator
{
    string GenerateFingerprint(SentryEvent sentryEvent);
}