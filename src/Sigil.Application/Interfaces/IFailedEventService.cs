using Sigil.Application.Models;
using Sigil.Application.Models.FailedEvents;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Application.Interfaces;

public interface IFailedEventService
{
    Task StoreAsync(int projectId, string rawEnvelope, FailedEventStage stage, Exception exception);
    Task<PagedResponse<FailedEventSummary>> GetFailedEventsAsync(int projectId, int page, int pageSize);
    Task<FailedEvent?> GetByIdAsync(long id);
    Task<bool> ReprocessAsync(long id);
    Task<int> ReprocessAllAsync(int projectId);
    Task<bool> DeleteAsync(long id);
}
