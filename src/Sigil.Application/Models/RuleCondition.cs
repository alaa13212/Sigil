using Sigil.Domain.Enums;

namespace Sigil.Application.Models;

public record RuleCondition(
    string Field,
    FilterOperator Operator,
    string Value);
