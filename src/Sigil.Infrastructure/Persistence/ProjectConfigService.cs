using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Domain;
using Sigil.Domain.Cors;

namespace Sigil.Infrastructure.Persistence;

internal class ProjectConfigService(IServiceProvider serviceProvider) : IProjectConfigService, IAsyncStartupInitializer
{
    private readonly ConcurrentDictionary<int, Dictionary<string, string?>> _store = new();
    private readonly ConcurrentDictionary<int, IReadOnlyList<string>> _corsOrigins = new();

    public Task InitializeAsync() => LoadAsync();
    
    public async Task LoadAsync()
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
        var all = await dbContext.ProjectConfigs.ToListAsync();

        _store.Clear();
        _corsOrigins.Clear();
        foreach (var group in all.GroupBy(c => c.ProjectId))
            _store[group.Key] = group.ToDictionary(c => c.Key, c => c.Value);
    }

    public async Task LoadAsync(int projectId)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
        var configs = await dbContext.ProjectConfigs
            .Where(c => c.ProjectId == projectId)
            .ToDictionaryAsync(c => c.Key, c => c.Value);

        _store[projectId] = configs;
        _corsOrigins.TryRemove(projectId, out _);
    }

    public int HighVolumeThreshold(int projectId) =>
        Get(projectId, ProjectConfigKeys.HighVolumeThreshold, 1000);

    public int? RateLimitMaxEventsPerWindow(int projectId) =>
        GetNullable<int>(projectId, ProjectConfigKeys.RateLimitMaxEventsPerWindow);

    public int? RetentionMaxAgeDays(int projectId) =>
        GetNullable<int>(projectId, ProjectConfigKeys.RetentionMaxAgeDays);

    public int? RetentionMaxEventCount(int projectId) =>
        GetNullable<int>(projectId, ProjectConfigKeys.RetentionMaxEventCount);

    public IReadOnlyList<string> AllowedCorsOrigins(int projectId)
    {
        if (_corsOrigins.TryGetValue(projectId, out var cached))
            return cached;

        var origins = CorsOrigins.Parse(Get(projectId, ProjectConfigKeys.AllowedCorsOrigins));
        _corsOrigins[projectId] = origins;
        return origins;
    }

    public string? Get(int projectId, string key) =>
        _store.TryGetValue(projectId, out var dict) ? dict.GetValueOrDefault(key) : null;

    public T Get<T>(int projectId, string key, T defaultValue) where T : IParsable<T>
    {
        var raw = Get(projectId, key);
        return raw is not null && T.TryParse(raw, null, out var parsed) ? parsed : defaultValue;
    }

    private T? GetNullable<T>(int projectId, string key) where T : struct, IParsable<T>
    {
        var raw = Get(projectId, key);
        return raw is not null && T.TryParse(raw, null, out var parsed) ? parsed : null;
    }
}
