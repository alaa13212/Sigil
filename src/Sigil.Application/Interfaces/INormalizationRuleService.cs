using Sigil.Application.Models.NormalizationRules;
using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface INormalizationRuleService
{
    List<TextNormalizationRule> CreateDefaultRulesPreset();
    
    Task<List<TextNormalizationRule>> GetRulesAsync(int projectId);
    Task<TextNormalizationRule> CreateRuleAsync(int projectId, CreateNormalizationRuleRequest request);
    Task<TextNormalizationRule?> UpdateRuleAsync(int ruleId, UpdateNormalizationRuleRequest request);
    Task<bool> DeleteRuleAsync(int ruleId);
    
    Task<List<TextNormalizationRule>> GetRawRulesAsync(int projectId);
}
