namespace Sigil.Application.Interfaces;

public interface IAppConfigService
{
    Task LoadAsync();

    string? HostUrl { get; }
    
    bool SetupComplete { get; }
    
    int RateLimitGlobalLimit { get; }
    int RateLimitDefaultProjectLimit { get; }
    int RateLimitWindowSeconds { get; }
    
    int RetentionDefaultMaxAgeDays { get; }
    int RetentionDefaultMaxEvents { get; }
    
    int RetentionCheckIntervalMinutes { get; }
    int RetentionFailedEnvelopeMaxAgeDays { get; }

    string? Get(string key);
    T Get<T>(string key, T defaultValue) where T : IParsable<T>;
}
