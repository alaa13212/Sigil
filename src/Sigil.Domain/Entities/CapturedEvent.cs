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
    public DateTime? ProcessedAt { get; set; }

    [MaxLength(8192)]
    public string? Message { get; set; }

    [MaxLength(500)]
    public string? ExceptionType { get; set; }

    [MaxLength(8192)]
    public string? Culprit { get; set; }

    public Severity Level { get; set; }

    [MaxLength(500)]
    public string? Logger { get; set; }

    public Platform Platform { get; set; }

    [ForeignKey(nameof(Release))]
    public int? ReleaseId { get; set; }
    public Release? Release { get; set; }
    

    public Dictionary<string, string>? Extra { get; set; }
    

    [ForeignKey(nameof(Issue))]
    public int IssueId { get; set; }
    
    [InverseProperty(nameof(Issue.Events))]
    public Issue? Issue { get; set; }
    
    [ForeignKey(nameof(Project))]
    public int ProjectId { get; set; }
    
    [InverseProperty(nameof(Project.Events))]
    public Project? Project { get; set; }

    [ForeignKey(nameof(User)), StringLength(64)]
    public string? UserId { get; set; }
    public EventUser? User { get; set; }

    public ICollection<StackFrame> StackFrames { get; set; } = [];
    
    [InverseProperty(nameof(TagValue.Events))]
    public ICollection<TagValue> Tags { get; set; } = [];
    

    [Required]
    public required byte[]? RawCompressedJson { get; set; }
}