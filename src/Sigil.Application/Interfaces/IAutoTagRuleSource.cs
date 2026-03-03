using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Provides raw auto-tag rules to the digestion pipeline.</summary>
public interface IAutoTagRuleSource
{
    Task<List<AutoTagRule>> GetRawRulesForProjectAsync(int projectId);
}
