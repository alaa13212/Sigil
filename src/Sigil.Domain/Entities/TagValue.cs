using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sigil.Domain.Entities;

public class TagValue
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(TagKey))]
    public int TagKeyId { get; set; }
    public TagKey? TagKey { get; set; }

    [Required, MaxLength(1000)]
    public required string Value { get; set; }

    [InverseProperty(nameof(CapturedEvent.Tags))]
    public ICollection<CapturedEvent> Events { get; set; } = [];
    
    public ICollection<IssueTag> IssueTags { get; set; } = [];
}