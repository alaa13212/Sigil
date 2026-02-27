using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Domain;

namespace Sigil.Infrastructure.Persistence;

internal class AppConfigService(IServiceProvider serviceProvider) : IAppConfigService
{
    private readonly ConcurrentDictionary<string, string?> _store = new();

    public async Task LoadAsync()
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
        var all = await dbContext.AppConfigs.ToDictionaryAsync(c => c.Key, c => c.Value);

        _store.Clear();
        foreach (var (key, value) in all)
            _store[key] = value;
    }

    public string? HostUrl => Get(AppConfigKeys.HostUrl);
    public bool SetupComplete => Get(AppConfigKeys.SetupComplete, false);
    
    public int RateLimitGlobalLimit => Get(AppConfigKeys.RateLimitGlobalLimit, 50_000);
    public int RateLimitDefaultProjectLimit => Get(AppConfigKeys.RateLimitDefaultProjectLimit, 50_000);
    public int RateLimitWindowSeconds => Get(AppConfigKeys.RateLimitWindowSeconds, 60);
    
    public int RetentionDefaultMaxAgeDays => Get(AppConfigKeys.RetentionDefaultMaxAgeDays, 90);
    public int RetentionDefaultMaxEvents => Get(AppConfigKeys.RetentionDefaultMaxEvents, 250_000);
    public int RetentionCheckIntervalMinutes => Get(AppConfigKeys.RetentionCheckIntervalMinutes, 60);
    public int RetentionFailedEnvelopeMaxAgeDays => Get(AppConfigKeys.RetentionFailedEnvelopeMaxAgeDays, 7);

    public string? Get(string key) => _store.GetValueOrDefault(key);

    public T Get<T>(string key, T defaultValue) where T : IParsable<T>
    {
        var raw = Get(key);
        return raw is not null && T.TryParse(raw, null, out var parsed) ? parsed : defaultValue;
    }
}
