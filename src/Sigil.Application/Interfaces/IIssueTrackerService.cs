using Sigil.Application.Models.IssueTrackers;

namespace Sigil.Application.Interfaces;

public interface IIssueTrackerService
{
    // Tracker config management
    Task<IssueTrackerConfigResponse> AddConfigAsync(int projectId, CreateTrackerConfigRequest request);
    Task<List<IssueTrackerConfigResponse>> GetConfigsAsync(int projectId);
    Task<bool> UpdateConfigAsync(int projectId, int configId, UpdateTrackerConfigRequest request);
    Task<bool> DeleteConfigAsync(int projectId, int configId);
    Task<bool> TestConfigAsync(int projectId, int configId);

    // External issue linking
    Task<ExternalIssueLinkResponse> CreateExternalIssueAsync(int issueId, int trackerConfigId);
    Task<List<ExternalIssueLinkResponse>> GetLinksAsync(int issueId);
    Task<bool> UnlinkAsync(int projectId, int linkId);
}
