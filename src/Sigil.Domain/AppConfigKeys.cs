namespace Sigil.Domain;

public static class AppConfigKeys
{
    public const string HostUrl = "host_url";
    public const string SetupComplete = "setup_complete";

    // Rate limiting
    public const string RateLimitGlobalLimit = "rate_limit_global_limit";
    public const string RateLimitDefaultProjectLimit = "rate_limit_default_project_limit";
    public const string RateLimitWindowSeconds = "rate_limit_window_seconds";

    // Retention
    public const string RetentionDefaultMaxAgeDays = "retention_default_max_age_days";
    public const string RetentionDefaultMaxEvents = "retention_default_max_events";
    public const string RetentionCheckIntervalMinutes = "retention_check_interval_minutes";
    public const string RetentionFailedEnvelopeMaxAgeDays = "retention_failed_envelope_max_age_days";
}
