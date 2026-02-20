using Sigil.Application.Models.AutoTags;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Interfaces;

public interface IAutoTagService
{
    Task<List<AutoTagRuleResponse>> GetRulesForProjectAsync(int projectId);
    Task<AutoTagRuleResponse> CreateRuleAsync(int projectId, CreateAutoTagRuleRequest request);
    Task<AutoTagRuleResponse?> UpdateRuleAsync(int ruleId, UpdateAutoTagRuleRequest request);
    Task<bool> DeleteRuleAsync(int ruleId);

    // Server-only (digestion pipeline)
    Task<List<AutoTagRule>> GetRawRulesForProjectAsync(int projectId);
    void ApplyRules(ParsedEvent parsedEvent, List<AutoTagRule> rules);
}
