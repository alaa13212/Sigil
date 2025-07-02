using System.Diagnostics.CodeAnalysis;
using Sigil.Core.Extensions;
using Sigil.Core.Parsing.Models;

namespace Sigil.Core.IssueGrouping;

public class FingerprintGenerator(IHashGenerator hashGenerator) : IFingerprintGenerator
{
    private const string DefaultFingerprintPlaceholder = "{{ default }}";
    

    public string GenerateFingerprint(SentryEvent sentryEvent)
    {
        List<string> fingerprintParts = ShouldUseCustomFingerprint(sentryEvent.Fingerprint) 
            ? ExtractEventFingerprintParts(sentryEvent) 
            : sentryEvent.Fingerprint;
        
        return hashGenerator.ComputeHash(string.Join("|", fingerprintParts));
    }

    private List<string> ExtractEventFingerprintParts(SentryEvent sentryEvent)
    {
        var parts = new List<string>();

        // Exception basics
        var exception = sentryEvent.Exception?.Values?.FirstOrDefault();
        parts.Add(exception?.Type ?? "unknown-exception");
        parts.Add(sentryEvent.GetMessage() ?? "no-message");

        // Stacktrace digest
        IEnumerable<SentryStackFrame>? frames = sentryEvent.GetStackFrames();

        if (frames != null)
        {
            if(frames.Any(f => f.InApp == true)) 
                frames = frames.Where(f => f.InApp == true);
            
            frames = frames.Where(f => !string.IsNullOrEmpty(f.Module) || !string.IsNullOrEmpty(f.Function));

            // Only keep top N frames for stability (e.g., top 5 app frames)
            parts.AddRange(frames.TakeLast(5).Select(frame => $"{frame.Function}@{frame.Filename}"));
        }
        
        InsertClientFingerprintComponents(parts, sentryEvent.Fingerprint);
        
        return parts;
    }

    private static void InsertClientFingerprintComponents(List<string> parts, List<string>? sentryEventFingerprint)
    {
        if(ShouldUseCustomFingerprint(sentryEventFingerprint))
            return;
            
        int i = 0;
        foreach (var fingerprintPart in sentryEventFingerprint)
        {
            if (fingerprintPart == DefaultFingerprintPlaceholder)
            {
                i = parts.Count;
            }
            else
            {
                parts.Insert(i++, fingerprintPart);
            }
        }
    }

    private static bool ShouldUseCustomFingerprint([NotNullWhen(false)] List<string>? sentryEventFingerprint)
    {
        return sentryEventFingerprint.IsNullOrEmpty() || !sentryEventFingerprint.Contains(DefaultFingerprintPlaceholder);
    }
}