using Sigil.Domain.Enums;

namespace Sigil.Application.Models;

public record PostDigestionWork(
    int ProjectId,
    List<int> IssueIds,
    HashSet<int> NewIssueIds,
    HashSet<int> RegressionIssueIds,
    List<EventBucketIncrement> BucketIncrements,
    List<PriorityChange> PriorityChanges);

public record EventBucketIncrement(int IssueId, DateTime BucketStart, int Count);

public record PriorityChange(int IssueId, Priority OldPriority, Priority NewPriority, string Reason);
