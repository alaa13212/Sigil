using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sigil.Domain.Entities;

public class StackFrame
{
    [Key]
    public int Id { get; set; }

    [MaxLength(500)]
    public string? Function { get; set; }

    [MaxLength(500)]
    public string? Filename { get; set; }

    public int? LineNumber { get; set; }
    public int? ColumnNumber { get; set; }

    [MaxLength(500)]
    public string? Module { get; set; }

    public bool InApp { get; set; }

    // SDK-provided source context (available for some platforms like Python/Ruby)
    [MaxLength(2000)]
    public string? ContextLine { get; set; }
    public string[]? PreContext { get; set; }
    public string[]? PostContext { get; set; }

    [ForeignKey(nameof(Event))]
    public long EventId { get; set; }
    public CapturedEvent? Event { get; set; }
}