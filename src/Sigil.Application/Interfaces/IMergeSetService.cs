using Sigil.Application.Models.MergeSets;

namespace Sigil.Application.Interfaces;

public interface IMergeSetService
{
    Task<MergeSetResponse> CreateAsync(int projectId, List<int> issueIds, Guid userId);
    Task<MergeSetResponse> BulkAddIssuesAsync(int mergeSetId, List<int> issueIds, Guid userId);
    Task RemoveIssueAsync(int mergeSetId, int issueId, Guid userId);
    Task SetPrimaryAsync(int mergeSetId, int issueId);
    Task<MergeSetResponse?> GetByIdAsync(int mergeSetId);
    Task RefreshAggregatesAsync(IEnumerable<int> mergeSetIds);
}
