using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Cache;

internal sealed class AutoTagRuleCache(ICacheManager cacheManager)
    : ProjectScopedCache<IAutoTagRuleCache, AutoTagRule>(cacheManager), IAutoTagRuleCache;
