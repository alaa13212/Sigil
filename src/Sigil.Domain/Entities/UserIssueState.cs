using System.ComponentModel.DataAnnotations.Schema;

namespace Sigil.Domain.Entities;

public class UserIssueState
{
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }
    public User? User { get; set; }

    [ForeignKey(nameof(Issue))]
    public int IssueId { get; set; }
    public Issue? Issue { get; set; }

    public bool IsBookmarked { get; set; }
    public DateTime? BookmarkedAt { get; set; }

    public DateTime? LastViewedAt { get; set; }
}
