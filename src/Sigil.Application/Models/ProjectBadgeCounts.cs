namespace Sigil.Application.Models;

public class ProjectBadgeCounts(int unseenIssues, int unseenReleases)
{
    public static readonly ProjectBadgeCounts Empty = new(0, 0);
    
    public int UnseenIssues { get; set; } = unseenIssues;
    public int UnseenReleases { get; set; } = unseenReleases;

    public void Deconstruct(out int unseenIssues, out int unseenReleases)
    {
        unseenIssues = UnseenIssues;
        unseenReleases = UnseenReleases;
    }
}
