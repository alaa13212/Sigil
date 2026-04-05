using Sigil.Domain.Enums;

namespace Sigil.Application.Models.IssueTrackers;

public record TrackerConfig(
    TrackerType TrackerType,
    string DecryptedConfigJson);

public record IssueTrackerConfigResponse(
    int Id,
    int ProjectId,
    TrackerType TrackerType,
    bool Enabled,
    bool TwoWaySync,
    DateTime CreatedAt);

public record CreateTrackerConfigRequest(
    TrackerType TrackerType,
    string ConfigJson);

public record UpdateTrackerConfigRequest(
    string? ConfigJson,
    bool? Enabled,
    bool? TwoWaySync);

public record ExternalIssueLinkResponse(
    int Id,
    int IssueId,
    TrackerType TrackerType,
    string ExternalId,
    string ExternalUrl,
    string? ExternalStatus,
    DateTime CreatedAt,
    DateTime? LastSyncedAt);

public record CreateExternalIssueRequest(
    string Title,
    string Description,
    string IssueUrl);

public record ExternalIssueResult(
    string ExternalId,
    string ExternalUrl,
    string? InitialStatus);

public record ExternalIssueStatus(
    string ExternalId,
    string Status,
    bool IsCompleted);
