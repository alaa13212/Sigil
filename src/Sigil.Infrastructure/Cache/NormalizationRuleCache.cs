using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Cache;

internal sealed class NormalizationRuleCache(ICacheManager cacheManager)
    : ProjectScopedCache<INormalizationRuleCache, TextNormalizationRule>(cacheManager), INormalizationRuleCache;
