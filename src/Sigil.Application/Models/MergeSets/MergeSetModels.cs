namespace Sigil.Application.Models.MergeSets;

public record MergeSetResponse(int Id, int PrimaryIssueId, List<MergeSetMember> Members, DateTime CreatedAt);

public record MergeSetMember(
    int IssueId,
    string? Title,
    string? ExceptionType,
    string Fingerprint,
    int OccurrenceCount,
    DateTime FirstSeen,
    DateTime LastSeen,
    bool IsPrimary);

public record CreateMergeSetRequest(List<int> IssueIds);

public record BulkAddIssuesToMergeSetRequest(List<int> IssueIds);

public record SetPrimaryRequest(int IssueId);
