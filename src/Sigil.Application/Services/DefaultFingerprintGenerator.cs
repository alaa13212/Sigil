using Sigil.Application.Interfaces;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class DefaultFingerprintGenerator(IHashGenerator hashGenerator) : IFingerprintGenerator
{
    private const string DefaultFingerprintPlaceholder = "{{ default }}";

    public string GenerateFingerprint(ParsedEvent parsedEvent)
    {
        var hints = parsedEvent.FingerprintHints;
        List<string> fingerprintParts;

        if (hints.IsNullOrEmpty())
        {
            // No custom hints → pure event-derived fingerprint
            fingerprintParts = ExtractEventFingerprintParts(parsedEvent);
        }
        else if (hints.Contains(DefaultFingerprintPlaceholder))
        {
            // "{{ default }}" present → blend event parts with client hints
            fingerprintParts = ExtractEventFingerprintParts(parsedEvent);
            InsertClientFingerprintComponents(fingerprintParts, hints);
        }
        else
        {
            // Pure client fingerprint (no {{ default }})
            fingerprintParts = hints.ToList();
        }

        return hashGenerator.ComputeHash(string.Join("|", fingerprintParts));
    }

    private List<string> ExtractEventFingerprintParts(ParsedEvent parsedEvent)
    {
        var parts = new List<string>();

        // Exception basics
        parts.Add(parsedEvent.ExceptionType ?? "unknown-exception");

        string message = parsedEvent.NormalizedMessage ?? "no-message";
        parts.Add(message);

        // Stacktrace digest
        IEnumerable<ParsedStackFrame> frames = parsedEvent.Stacktrace;

        if(frames.Any(f => f.InApp))
            frames = frames.Where(f => f.InApp);

        frames = frames
            .Where(f => !f.Filename.IsNullOrEmpty())
            .Where(f => !f.Function.IsNullOrEmpty());

        // Only keep top N frames for stability (e.g., top 5 app frames)
        parts.AddRange(frames.TakeLast(5).Select(frame => $"{frame.Function}@{frame.Filename}"));

        return parts;
    }

    private static void InsertClientFingerprintComponents(List<string> parts, IReadOnlyList<string> fingerprintHints)
    {
        int i = 0;
        foreach (string fingerprintHint in fingerprintHints)
        {
            if (fingerprintHint == DefaultFingerprintPlaceholder)
                i = parts.Count; // {{ default }} → set insert point to end of event parts
            else
                parts.Insert(i++, fingerprintHint);
        }
    }
}
