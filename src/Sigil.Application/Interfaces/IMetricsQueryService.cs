using Sigil.Application.Models.Metrics;

namespace Sigil.Application.Interfaces;

/// <summary>Gathers the database-backed values exposed by the metrics endpoint.</summary>
public interface IMetricsQueryService
{
    /// <summary>Builds a snapshot of issue, event, alert delivery and backlog values.</summary>
    Task<MetricsSnapshot> GetSnapshotAsync(CancellationToken ct = default);
}
