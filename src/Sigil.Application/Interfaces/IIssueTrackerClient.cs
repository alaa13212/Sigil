using Sigil.Application.Models.IssueTrackers;
using Sigil.Domain.Enums;

namespace Sigil.Application.Interfaces;

public interface IIssueTrackerClient
{
    TrackerType TrackerType { get; }
    Task<ExternalIssueResult> CreateIssueAsync(TrackerConfig config, CreateExternalIssueRequest request);
    Task<ExternalIssueStatus?> GetStatusAsync(TrackerConfig config, string externalId);
    Task<bool> CloseIssueAsync(TrackerConfig config, string externalId);
    Task<bool> TestConnectionAsync(TrackerConfig config);
}
