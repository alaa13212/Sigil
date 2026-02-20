namespace Sigil.Application.Models;

public record PostDigestionWork(
    int ProjectId,
    List<int> IssueIds,
    HashSet<int> NewIssueIds,
    HashSet<int> RegressionIssueIds);
