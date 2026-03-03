using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Provides raw normalization rules and presets to the digestion pipeline and setup service.</summary>
public interface INormalizationRuleEngine
{
    List<TextNormalizationRule> CreateDefaultRulesPreset();
    Task<List<TextNormalizationRule>> GetRawRulesAsync(int projectId);
}
