namespace Sigil.Application.Models.Digestion;

public class DigestionStats
{
    public int PendingCount { get; init; }
    public int FailedCount { get; init; }
    public int BatchSize { get; init; }
    public DateTime? OldestPendingAt { get; init; }
    public List<ProjectEnvelopeStats> ByProject { get; init; } = [];

    // Throughput metrics (rolling 60-minute window)
    public double EventsPerMinute { get; init; }
    public double AvgDigestMs { get; init; }
    public double AvgParseMs { get; init; }
    public int PeakQueueDepth { get; init; }
    public int BatchesInLastHour { get; init; }
}

public class ProjectEnvelopeStats
{
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public int PendingCount { get; init; }
    public int FailedCount { get; init; }
}

public class FailedEnvelopeSummary
{
    public long Id { get; init; }
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public DateTime ReceivedAt { get; init; }
    public string? Error { get; init; }
}
