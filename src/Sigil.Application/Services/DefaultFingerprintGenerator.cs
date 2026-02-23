using System.Diagnostics.CodeAnalysis;
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
        IReadOnlyList<string> fingerprintParts = ShouldUseCustomFingerprint(parsedEvent.FingerprintHints) 
            ? ExtractEventFingerprintParts(parsedEvent) 
            : parsedEvent.FingerprintHints;
        
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

        InsertClientFingerprintComponents(parts, parsedEvent.FingerprintHints);
        
        return parts;
    }

    private static void InsertClientFingerprintComponents(List<string> parts, IReadOnlyList<string>? fingerprintHints)
    {
        if(ShouldUseCustomFingerprint(fingerprintHints))
            return;
            
        int i = 0;
        foreach (string fingerprintHint in fingerprintHints)
        {
            if (fingerprintHint == DefaultFingerprintPlaceholder)
            {
                i = parts.Count;
            }
            else
            {
                parts.Insert(i++, fingerprintHint);
            }
        }
    }

    private static bool ShouldUseCustomFingerprint([NotNullWhen(false)] IReadOnlyList<string>? fingerprintHints)
    {
        return fingerprintHints.IsNullOrEmpty() || !fingerprintHints.Contains(DefaultFingerprintPlaceholder);
    }
}