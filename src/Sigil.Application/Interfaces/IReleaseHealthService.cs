using Sigil.Application.Models;
using Sigil.Application.Models.Releases;

namespace Sigil.Application.Interfaces;

public interface IReleaseHealthService
{
    Task<PagedResponse<ReleaseHealthSummary>> GetReleaseHealthAsync(int projectId, int page = 1, int pageSize = 20);
    Task<ReleaseDetailResponse?> GetReleaseDetailAsync(int releaseId);
}
