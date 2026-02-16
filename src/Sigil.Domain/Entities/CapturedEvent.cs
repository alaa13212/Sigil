using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class CapturedEvent
{
    [Key]
    public long Id { get; set; }

    [Required, StringLength(32)]
    public required string EventId { get; set; }

    public required DateTime Timestamp { get; set; }
    public required DateTime ReceivedAt { get; set; }

    [MaxLength(1000)]
    public string? Message { get; set; }

    public Severity Level { get; set; }

    [MaxLength(500)]
    public string? Logger { get; set; }

    public Platform Platform { get; set; }

    [ForeignKey(nameof(Release))]
    public int ReleaseId { get; set; }
    public Release? Release { get; set; }
    

    [Column(TypeName = "jsonb")]
    public Dictionary<string, string>? Extra { get; set; }
    

    [ForeignKey(nameof(Issue))]
    public int IssueId { get; set; }
    
    [InverseProperty(nameof(Issue.Events))]
    public Issue? Issue { get; set; }

    [ForeignKey(nameof(User)), StringLength(64)]
    public string? UserId { get; set; }
    public EventUser? User { get; set; }

    public ICollection<StackFrame> StackFrames { get; set; } = [];
    
    [InverseProperty(nameof(TagValue.Events))]
    public ICollection<TagValue> Tags { get; set; } = [];
    

    [Required]
    public required byte[]? RawCompressedJson { get; set; }
}