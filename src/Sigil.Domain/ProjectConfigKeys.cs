namespace Sigil.Domain;

public static class ProjectConfigKeys
{
    public const string HighVolumeThreshold = "high_volume_threshold";

    // Per-project rate limit override (null = use global default from AppConfig)
    public const string RateLimitMaxEventsPerWindow = "rate_limit_max_events_per_window";

    // Per-project retention overrides (null = use global defaults from AppConfig)
    public const string RetentionMaxAgeDays = "retention_max_age_days";
    public const string RetentionMaxEventCount = "retention_max_event_count";
}
