using Sigil.Application.Models.IssueTrackers;

namespace Sigil.Application.Interfaces;

public interface IIssueTrackerService
{
    // Tracker config management
    Task<IssueTrackerConfigResponse> AddConfigAsync(int projectId, CreateTrackerConfigRequest request);
    Task<List<IssueTrackerConfigResponse>> GetConfigsAsync(int projectId);
    Task<bool> UpdateConfigAsync(int configId, UpdateTrackerConfigRequest request);
    Task<bool> DeleteConfigAsync(int configId);
    Task<bool> TestConfigAsync(int configId);

    // External issue linking
    Task<ExternalIssueLinkResponse> CreateExternalIssueAsync(int issueId, int trackerConfigId);
    Task<List<ExternalIssueLinkResponse>> GetLinksAsync(int issueId);
    Task<bool> UnlinkAsync(int linkId);
}
