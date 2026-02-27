using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Domain;

namespace Sigil.Infrastructure.Persistence;

internal class ProjectConfigService(IServiceProvider serviceProvider) : IProjectConfigService
{
    private readonly ConcurrentDictionary<int, Dictionary<string, string?>> _store = new();

    public async Task LoadAsync()
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
        var all = await dbContext.ProjectConfigs.ToListAsync();

        _store.Clear();
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
    }

    public int HighVolumeThreshold(int projectId) =>
        Get(projectId, ProjectConfigKeys.HighVolumeThreshold, 1000);

    public int? RateLimitMaxEventsPerWindow(int projectId) =>
        GetNullable<int>(projectId, ProjectConfigKeys.RateLimitMaxEventsPerWindow);

    public int? RetentionMaxAgeDays(int projectId) =>
        GetNullable<int>(projectId, ProjectConfigKeys.RetentionMaxAgeDays);

    public int? RetentionMaxEventCount(int projectId) =>
        GetNullable<int>(projectId, ProjectConfigKeys.RetentionMaxEventCount);

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
