using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sigil.Domain.Entities;

public class SharedIssueLink
{
    [Key]
    public Guid Token { get; set; }

    [ForeignKey(nameof(Issue))]
    public int IssueId { get; set; }
    public Issue? Issue { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
