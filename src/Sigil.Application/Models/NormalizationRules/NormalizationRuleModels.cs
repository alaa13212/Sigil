namespace Sigil.Application.Models.NormalizationRules;

public record CreateNormalizationRuleRequest(
    string Pattern,
    string Replacement,
    int Priority,
    bool Enabled,
    string? Description);

public record UpdateNormalizationRuleRequest(
    string Pattern,
    string Replacement,
    int Priority,
    bool Enabled,
    string? Description);
