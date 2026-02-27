using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Sigil.Domain.Enums;

namespace Sigil.Domain.Entities;

public class Issue
{
    [Key]
    public int Id { get; set; }

    [MaxLength(1000)]
    public string? Title { get; set; }
    
    [Required, StringLength(64)]
    public required string Fingerprint { get; set; }

    [MaxLength(500)]
    public string? ExceptionType { get; set; }

    [MaxLength(500)]
    public string? Culprit { get; set; }

    public IssueStatus Status { get; set; }
    public Priority Priority { get; set; }

    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime LastChangedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedInReleaseId { get; set; }
    public int OccurrenceCount { get; set; }
    
    public Severity Level { get; set; }

    [ForeignKey(nameof(Project))]
    public required int ProjectId { get; set; }
    public Project? Project { get; set; }
    
    [InverseProperty(nameof(CapturedEvent.Issue))]
    public ICollection<CapturedEvent> Events { get; set; } = [];
    
    [InverseProperty(nameof(IssueTag.Issue))]
    public ICollection<IssueTag> Tags { get; set; } = [];
    
    [ForeignKey(nameof(SuggestedEvent))]
    public long? SuggestedEventId { get; set; }
    public CapturedEvent? SuggestedEvent { get; set; }
    
    [ForeignKey(nameof(ResolvedBy))]
    public Guid? ResolvedById { get; set; }
    public User? ResolvedBy { get; set; }
    
    [ForeignKey(nameof(AssignedTo))]
    public Guid? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }

    [InverseProperty(nameof(IssueActivity.Issue))]
    public ICollection<IssueActivity> Activities { get; set; } = [];

    [ForeignKey(nameof(MergeSet))]
    public int? MergeSetId { get; set; }
    public MergeSet? MergeSet { get; set; }

    // Set when the issue is ignored with "ignore future events" option
    [ForeignKey(nameof(IgnoreFilter))]
    public int? IgnoreFilterId { get; set; }
    public EventFilter? IgnoreFilter { get; set; }

    // Denormalized for full-text search
    [MaxLength(1000)]
    public string? SuggestedEventMessage { get; set; }
    [MaxLength(2000)]
    public string? SuggestedFramesSummary { get; set; }
}