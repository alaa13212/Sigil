using System.Text.RegularExpressions;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Services;

public class RuleEngine : IRuleEngine
{
    public bool Evaluate(RuleCondition condition, ParsedEvent evt)
    {
        var fieldValue = ResolveField(condition.Field, evt);
        return fieldValue is not null && MatchOperator(fieldValue, condition.Operator, condition.Value);
    }

    public bool EvaluateAll(IEnumerable<RuleCondition> conditions, ParsedEvent evt, bool requireAll = true)
    {
        return requireAll
            ? conditions.All(c => Evaluate(c, evt))
            : conditions.Any(c => Evaluate(c, evt));
    }

    private static string? ResolveField(string field, ParsedEvent evt) => field switch
    {
        "exceptionType"      => evt.ExceptionType,
        "message"            => evt.Message,
        "normalizedMessage"  => evt.NormalizedMessage,
        "release"            => evt.Release,
        "environment"        => evt.Environment,
        "logger"             => evt.Logger,
        "level"              => evt.Level.ToString(),
        "culprit"            => evt.Culprit,
        "serverName"         => evt.ServerName,
        "fingerprint"        => evt.Fingerprint,
        "stacktrace"         => evt.Stacktrace.Count > 0 ? string.Join("\n", evt.Stacktrace.Select(f => f.Function)) : null,
        not null when field.StartsWith("tag:") => evt.Tags?.GetValueOrDefault(field[4..]),
        
        _ => null
    };

    public bool Match(string value, FilterOperator op, string pattern) => MatchOperator(value, op, pattern);

    private static bool MatchOperator(string fieldValue, FilterOperator op, string pattern) => op switch
    {
        FilterOperator.Equals     => string.Equals(fieldValue, pattern, StringComparison.OrdinalIgnoreCase),
        FilterOperator.Contains   => fieldValue.Contains(pattern, StringComparison.OrdinalIgnoreCase),
        FilterOperator.StartsWith => fieldValue.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
        FilterOperator.EndsWith   => fieldValue.EndsWith(pattern, StringComparison.OrdinalIgnoreCase),
        FilterOperator.Regex      => IsRegexMatch(fieldValue, pattern),
        _ => false
    };

    private static bool IsRegexMatch(string input, string pattern)
    {
        try { return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)); }
        catch { return false; }
    }
}
