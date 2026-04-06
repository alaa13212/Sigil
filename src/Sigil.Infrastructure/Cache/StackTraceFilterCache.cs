using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Cache;

internal sealed class StackTraceFilterCache(ICacheManager cacheManager)
    : ProjectScopedCache<IStackTraceFilterCache, StackTraceFilter>(cacheManager), IStackTraceFilterCache;
