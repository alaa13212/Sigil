using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IRawEnvelopeService
{
    Task StoreAsync(int projectId, string rawData, DateTime receivedAt);
    Task BulkStoreAsync(IEnumerable<(int ProjectId, string RawData, DateTime ReceivedAt)> items);
    Task<List<RawEnvelope>> FetchUnprocessedAsync(int batchSize);
    Task DeleteAsync(IEnumerable<long> ids);
    Task BulkMarkFailedAsync(IEnumerable<(long Id, string Error)> failures);
}
