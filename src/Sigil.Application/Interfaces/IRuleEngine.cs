using Sigil.Application.Models;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IRuleEngine
{
    bool Evaluate(RuleCondition condition, ParsedEvent evt);
    bool EvaluateAll(IEnumerable<RuleCondition> conditions, ParsedEvent evt, bool requireAll = true);
    bool Match(string value, FilterOperator op, string pattern);
}