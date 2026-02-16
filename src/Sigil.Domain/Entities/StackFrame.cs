using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sigil.Domain.Entities;

public class StackFrame
{
    [Key]
    public int Id { get; set; }

    [MaxLength(300)]
    public string? Function { get; set; }

    [MaxLength(300)]
    public string? Filename { get; set; }

    public int? LineNumber { get; set; }
    public int? ColumnNumber { get; set; }

    [MaxLength(300)]
    public string? Module { get; set; }

    public bool InApp { get; set; }

    [ForeignKey(nameof(Event))]
    public long EventId { get; set; }
    public CapturedEvent? Event { get; set; }
}