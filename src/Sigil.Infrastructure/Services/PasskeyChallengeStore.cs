using System.Collections.Concurrent;

namespace Sigil.Infrastructure.Services;

internal class PasskeyChallengeStore
{
    private readonly ConcurrentDictionary<string, (string Json, DateTime Expiry)> _store = new();
    private DateTime _lastCleanup = DateTime.UtcNow;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);

    public void Store(string key, string json)
    {
        CleanupIfNeeded();
        _store[key] = (json, DateTime.UtcNow + Ttl);
    }

    public string? Get(string key)
    {
        if (!_store.TryRemove(key, out var entry))
            return null;

        return entry.Expiry < DateTime.UtcNow ? null : entry.Json;
    }

    private void CleanupIfNeeded()
    {
        var now = DateTime.UtcNow;
        if (now - _lastCleanup < CleanupInterval)
            return;

        _lastCleanup = now;
        foreach (var kvp in _store)
        {
            if (kvp.Value.Expiry < now)
                _store.TryRemove(kvp.Key, out _);
        }
    }
}
