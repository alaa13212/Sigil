namespace Sigil.Application.Interfaces;

public interface IProjectConfigService
{
    Task LoadAsync();
    Task LoadAsync(int projectId);

    int HighVolumeThreshold(int projectId);
    int? RateLimitMaxEventsPerWindow(int projectId);
    int? RetentionMaxAgeDays(int projectId);
    int? RetentionMaxEventCount(int projectId);

    string? Get(int projectId, string key);
    T Get<T>(int projectId, string key, T defaultValue) where T : IParsable<T>;
}
