using Sigil.Application.Models.AutoTags;

namespace Sigil.Application.Interfaces;

public interface IAutoTagService
{
    Task<List<AutoTagRuleResponse>> GetRulesForProjectAsync(int projectId);
    Task<AutoTagRuleResponse> CreateRuleAsync(int projectId, CreateAutoTagRuleRequest request);
    Task<AutoTagRuleResponse?> UpdateRuleAsync(int ruleId, UpdateAutoTagRuleRequest request);
    Task<bool> DeleteRuleAsync(int ruleId);
}
