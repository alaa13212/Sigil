using Sigil.Domain.Enums;

namespace Sigil.Application.Models.AutoTags;

public record AutoTagRuleResponse(
    int Id,
    int ProjectId,
    string Field,
    FilterOperator Operator,
    string Value,
    string TagKey,
    string TagValue,
    bool Enabled,
    int Priority,
    string? Description,
    DateTime CreatedAt);

public record CreateAutoTagRuleRequest(
    string Field,
    FilterOperator Operator,
    string Value,
    string TagKey,
    string TagValue,
    bool Enabled = true,
    int Priority = 0,
    string? Description = null);

public record UpdateAutoTagRuleRequest(
    string Field,
    FilterOperator Operator,
    string Value,
    string TagKey,
    string TagValue,
    bool Enabled,
    int Priority,
    string? Description);
