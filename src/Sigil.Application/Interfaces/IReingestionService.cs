using Sigil.Application.Models.Reingestion;

namespace Sigil.Application.Interfaces;

public interface IReingestionService
{
    Task<ReingestionJobResponse> StartProjectReingestionAsync(int projectId, Guid? userId = null);
    Task<ReingestionJobResponse> StartIssueReingestionAsync(int issueId, Guid? userId = null);
    Task<ReingestionJobResponse?> GetJobStatusAsync(int jobId);
    Task<List<ReingestionJobResponse>> GetJobsForProjectAsync(int projectId);
    Task<bool> CancelJobAsync(int jobId);
}
