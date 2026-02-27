using System.Collections.Concurrent;
using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Services;

internal class SlidingWindowRateLimiter(IAppConfigService appConfig) : IRateLimiter
{
    private readonly ConcurrentDictionary<int, WindowCounter> _projectCounters = new();
    private readonly WindowCounter _globalCounter = new();

    public bool TryAcquire(int projectId, int? projectLimit = null)
    {
        int windowSeconds = appConfig.RateLimitWindowSeconds;
        int globalLimit = appConfig.RateLimitGlobalLimit;
        int defaultProjectLimit = appConfig.RateLimitDefaultProjectLimit;
        int effectiveProjectLimit = projectLimit ?? defaultProjectLimit;

        TimeSpan window = TimeSpan.FromSeconds(windowSeconds);
        WindowCounter counter = _projectCounters.GetOrAdd(projectId, _ => new WindowCounter());

        if (!counter.TryAcquire(effectiveProjectLimit, window))
            return false;

        if (!_globalCounter.TryAcquire(globalLimit, window))
            return false;

        return true;
    }

    private sealed class WindowCounter
    {
        private readonly Lock _lock = new();
        private int _count;
        private DateTime _windowStart = DateTime.UtcNow;

        public bool TryAcquire(int limit, TimeSpan window)
        {
            lock (_lock)
            {
                DateTime now = DateTime.UtcNow;
                if (now - _windowStart > window)
                {
                    _windowStart = now;
                    _count = 0;
                }
                if (_count >= limit) return false;
                _count++;
                return true;
            }
        }
    }
}
