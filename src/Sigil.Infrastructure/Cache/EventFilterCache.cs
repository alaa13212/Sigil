using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Cache;

internal sealed class EventFilterCache(ICacheManager cacheManager)
    : ProjectScopedCache<IEventFilterCache, EventFilter>(cacheManager), IEventFilterCache;
