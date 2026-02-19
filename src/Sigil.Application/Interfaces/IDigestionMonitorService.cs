using Sigil.Application.Models.Digestion;

namespace Sigil.Application.Interfaces;

public interface IDigestionMonitorService
{
    Task<DigestionStats> GetStatsAsync();
    Task<List<FailedEnvelopeSummary>> GetRecentFailuresAsync(int limit = 50);
    Task<int> RetryFailedAsync(IEnumerable<long>? ids = null);
}
