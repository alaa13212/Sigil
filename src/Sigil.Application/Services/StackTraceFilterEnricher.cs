using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class StackTraceFilterEnricher(IRuleEngine ruleEngine) : IEventEnricher
{
    public void Enrich(ParsedEvent parsedEvent, EventParsingContext context)
    {
        if (context.StackTraceFilters.Count == 0 || parsedEvent.Stacktrace.Count == 0)
            return;

        parsedEvent.Stacktrace = parsedEvent.Stacktrace
            .Where(frame => !ShouldRemoveFrame(frame, context.StackTraceFilters))
            .ToList();
    }

    private bool ShouldRemoveFrame(ParsedStackFrame frame, List<StackTraceFilter> filters)
    {
        foreach (var filter in filters)
        {
            if (!filter.Enabled) continue;

            string? fieldValue = filter.Field switch
            {
                "function" => frame.Function,
                "filename" => frame.Filename,
                "module"   => frame.Module,
                _ => null
            };

            if (fieldValue is null) continue;

            if (ruleEngine.Match(fieldValue, filter.Operator, filter.Value))
                return true;
        }

        return false;
    }
}
