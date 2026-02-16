using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class DefaultMessageNormalizer : IMessageNormalizer
{
    private static readonly List<NormalizationRule> NormalizationRules =
    [
        // Specific typed values first (so they're not caught by generic patterns)
        new (@"\b(?:\d{1,3}\.){3}\d{1,3}(:\d{4,6})?\b", "{ip}"),
        new (@"\b[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\b", "{uuid}"),
        new (@"\b\d{4}-\d{2}-\d{2}(?:[ T]\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?)?\b", "{datetime}"),
        new (@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[A-Za-z]{2,}", "{email}"),
        new (@"https?:\/\/[^\s/$.?#].[^\s]*", "{url}"),
        new (@"/[A-Za-z0-9\-_]+(?:/[A-Za-z0-9\-_]+)*", "{urlpath}"),
        new ("""(?:(?:[A-Za-z]:\\|\\\\)[^\\/:*?"<>|\r\n]+(?:\\[^\\/:*?"<>|\r\n]+)*|/(?:[^/\0]+/)*[^/\0]*)""", "{filepath}"),

        new (@"\b[0-9A-Fa-f]{8,}\b", "{hex}"),
        new (@"\b\d{10}\b", "{epoch}"),
        new (@"\b\d{13}\b", "{epochms}"),
        
        new (@"\b(true|false)\b", "{bool}", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Generic key-value pattern: "Key: value," or "Key: value)" or "Key: value]" or "Key: value}"
        // This normalizes variable values in structured messages
        new (@"(:\s*)[^,(){}\[\]]+\b(?=\s*[,)}\]])", "$1{val}"),

        // Generic numbers
        new (@"\b\d+\b", "{int}"),

        // Quoted strings
        new (@"""([^""\{\}]*?)""", "\"{str}\""),
        new (@"'([^'\{\}]*?)'", "'{str}'"),
    ];

    
    public string NormalizeMessage(string message)
    {
        return NormalizationRules.Aggregate(message, (current, rule) => rule.NormalizeMessage(current));
    }
}

public class NormalizationRule([StringSyntax("regex")] string pattern, string replacement, RegexOptions options = RegexOptions.Compiled)
{
    public Regex Pattern { get; } = new(pattern, options);
    public string Replacement { get; } = replacement;

    public string NormalizeMessage(string message)
    {
        return Pattern.Replace(message, Replacement);
    }
}