using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sigil.Domain.Entities;

public class IssueTag
{
    [Required, ForeignKey(nameof(Issue))]
    public int IssueId { get; set; }
    public Issue? Issue { get; set; }

    [Required, ForeignKey(nameof(TagValue))]
    public int TagValueId { get; set; }
    public TagValue? TagValue { get; set; }

    public int OccurrenceCount { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}