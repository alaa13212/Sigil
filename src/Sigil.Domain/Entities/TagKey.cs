using System.ComponentModel.DataAnnotations;

namespace Sigil.Domain.Entities;

public class TagKey
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(500)]
    public required string Key { get; set; }

    public ICollection<TagValue> Values { get; set; } = [];
}