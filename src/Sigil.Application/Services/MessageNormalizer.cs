using System.Text.RegularExpressions;
using Sigil.Domain.Entities;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class MessageNormalizer : IMessageNormalizer
{
    private static readonly Dictionary<string, Regex> CompiledPatternsCache = [];
    
    public string NormalizeMessage(IReadOnlyCollection<TextNormalizationRule> rules, string message)
    {
        return rules.Aggregate(message, (current, rule) => GetCompiledPattern(rule).Replace(current, rule.Replacement));
    }

    private static Regex GetCompiledPattern(TextNormalizationRule rule)
    {
        if (CompiledPatternsCache.TryGetValue(rule.Pattern, out Regex? pattern))
            return pattern;
        
        return CompiledPatternsCache[rule.Pattern] = new Regex(rule.Pattern, RegexOptions.Compiled);
    }
}
