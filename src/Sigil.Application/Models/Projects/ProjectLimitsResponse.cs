namespace Sigil.Application.Models.Projects;

/// <summary>Rate limit and retention settings that apply to a project.</summary>
/// <param name="RetentionMaxAgeDaysOverride">Age limit set on the project, or null when it inherits the global default.</param>
/// <param name="RetentionMaxAgeDays">Age limit the retention worker enforces for this project.</param>
/// <param name="RetentionMaxEventCountOverride">Event cap set on the project, or null when it inherits the global default.</param>
/// <param name="RetentionMaxEvents">Event cap the retention worker enforces for this project.</param>
/// <param name="RetentionDefaultMaxAgeDays">Global age limit a project inherits when it has no override.</param>
/// <param name="RetentionDefaultMaxEvents">Global event cap a project inherits when it has no override.</param>
/// <param name="RetentionCheckIntervalMinutes">Minutes between retention sweeps.</param>
/// <param name="RetentionFailedEnvelopeMaxAgeDays">Days a failed ingestion envelope is kept before it is deleted.</param>
/// <param name="RateLimitMaxEventsPerWindowOverride">Event cap per rate limit window set on the project, or null when it inherits the global default.</param>
/// <param name="RateLimitDefaultProjectLimit">Event cap per rate limit window a project inherits when it has no override.</param>
public record ProjectLimitsResponse(
    int? RetentionMaxAgeDaysOverride,
    int RetentionMaxAgeDays,
    int? RetentionMaxEventCountOverride,
    int RetentionMaxEvents,
    int RetentionDefaultMaxAgeDays,
    int RetentionDefaultMaxEvents,
    int RetentionCheckIntervalMinutes,
    int RetentionFailedEnvelopeMaxAgeDays,
    int? RateLimitMaxEventsPerWindowOverride,
    int RateLimitDefaultProjectLimit);
