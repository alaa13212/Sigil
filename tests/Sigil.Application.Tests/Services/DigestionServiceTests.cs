using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Services;
using Sigil.Domain;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Tests.Services;

public class DigestionServiceTests
{
    private readonly IProjectEntityAccess _projectAccess = Substitute.For<IProjectEntityAccess>();
    private readonly IIssueIngestionService _issueIngestion = Substitute.For<IIssueIngestionService>();
    private readonly IEventIngestionService _eventIngestion = Substitute.For<IEventIngestionService>();
    private readonly IReleaseService _releaseService = Substitute.For<IReleaseService>();
    private readonly IEventUserService _eventUserService = Substitute.For<IEventUserService>();
    private readonly ITagService _tagService = Substitute.For<ITagService>();
    private readonly IEventRanker _eventRanker = Substitute.For<IEventRanker>();
    private readonly IEventFilterEngine _eventFilterEngine = Substitute.For<IEventFilterEngine>();
    private readonly IWorker<PostDigestionWork> _postDigestionQueue = Substitute.For<IWorker<PostDigestionWork>>();
    private readonly IDateTime _dateTime = Substitute.For<IDateTime>();

    private readonly DateTime _now = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly Project _project = new() { Id = 1, Name = "Test", Platform = Platform.CSharp, ApiKey = "key" };

    private DigestionService CreateService() => new(
        _projectAccess, _issueIngestion, _eventIngestion,
        _releaseService, _eventUserService, _tagService,
        _eventRanker, _eventFilterEngine, _postDigestionQueue, _dateTime);

    private EventParsingContext DefaultContext() => new()
    {
        ProjectId = _project.Id,
        NormalizationRules = [],
        AutoTagRules = [],
        InboundFilters = [],
        StackTraceFilters = [],
        HighVolumeThreshold = 1000,
    };

    private void SetupDefaults(List<Issue>? issues = null)
    {
        _dateTime.UtcNow.Returns(_now);
        _projectAccess.GetProjectByIdAsync(_project.Id).Returns(_project);
        _eventIngestion.FindExistingEventIdsAsync(Arg.Any<IEnumerable<string>>()).Returns([]);
        _releaseService.BulkGetOrCreateReleasesAsync(Arg.Any<int>(), Arg.Any<List<ParsedEvent>>()).Returns([]);
        _eventUserService.BulkGetOrCreateEventUsersAsync(Arg.Any<IReadOnlyCollection<ParsedEventUser>>()).Returns([]);

        // System tags need to be returned
        var systemTagValues = SystemTags.AllPairs.Select((p, i) => new TagValue
        {
            Id = 1000 + i, Value = p.Value, TagKey = new TagKey { Key = p.Key }
        }).ToList();
        _tagService.BulkGetOrCreateTagsAsync(Arg.Any<IReadOnlyCollection<KeyValuePair<string, string>>>())
            .Returns(ci =>
            {
                var requested = ci.Arg<IReadOnlyCollection<KeyValuePair<string, string>>>();
                var result = new List<TagValue>(systemTagValues);
                int nextId = 2000;
                foreach (var pair in requested.Where(p => !SystemTags.IsSystemTag(p.Key)))
                {
                    result.Add(new TagValue { Id = nextId++, Value = pair.Value, TagKey = new TagKey { Key = pair.Key } });
                }
                return (IReadOnlyCollection<TagValue>)result;
            });

        _issueIngestion.BulkGetOrCreateIssuesAsync(Arg.Any<Project>(), Arg.Any<IEnumerable<IGrouping<string, ParsedEvent>>>())
            .Returns(ci => issues ?? []);
        _eventIngestion.BulkCreateEventsEntities(Arg.Any<IEnumerable<ParsedEvent>>(), Arg.Any<Project>(), Arg.Any<Issue>(),
            Arg.Any<Dictionary<string, Release>>(), Arg.Any<Dictionary<string, EventUser>>(),
            Arg.Any<Dictionary<string, Dictionary<string, int>>>()).Returns([]);
        _eventRanker.GetMostRelevantEvent(Arg.Any<IEnumerable<CapturedEvent>>()).Returns(ci =>
        {
            var events = ci.Arg<IEnumerable<CapturedEvent>>();
            return events.FirstOrDefault()!;
        });
        _eventIngestion.SaveEventsAsync().Returns(true);
        _postDigestionQueue.TryEnqueue(Arg.Any<PostDigestionWork>()).Returns(true);
    }

    [Fact]
    public async Task BulkDigest_DuplicateEventIds_AreSkipped()
    {
        SetupDefaults();
        _eventIngestion.FindExistingEventIdsAsync(Arg.Any<IEnumerable<string>>())
            .Returns(["evt-1"]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.DidNotReceive().TryEnqueue(Arg.Any<PostDigestionWork>());
    }

    [Fact]
    public async Task BulkDigest_InboundFilterRejectsAll_NothingProcessed()
    {
        SetupDefaults();
        var context = DefaultContext();
        context = new EventParsingContext
        {
            ProjectId = _project.Id,
            NormalizationRules = [],
            AutoTagRules = [],
            InboundFilters = [new EventFilter { Id = 1, ProjectId = _project.Id, Field = "level", Operator = FilterOperator.Equals, Value = "info", Enabled = true }],
            StackTraceFilters = [],
        };
        _eventFilterEngine.ShouldRejectEvent(Arg.Any<ParsedEvent>(), Arg.Any<List<EventFilter>>()).Returns(true);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(context, events);

        _postDigestionQueue.DidNotReceive().TryEnqueue(Arg.Any<PostDigestionWork>());
    }

    [Fact]
    public async Task BulkDigest_NewIssues_PostDigestionContainsNewIssueIds()
    {
        var issue = new Issue
        {
            Id = 42, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low,
            OccurrenceCount = 0, // new issue
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.NewIssueIds.Contains(42)));
    }

    [Fact]
    public async Task BulkDigest_ResolvedIssue_DetectedAsRegression()
    {
        var issue = new Issue
        {
            Id = 10, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Resolved, Priority = Priority.Low,
            OccurrenceCount = 5,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.RegressionIssueIds.Contains(10)));
    }

    [Fact]
    public async Task BulkDigest_RegressionIssue_PriorityElevatedToMedium()
    {
        var issue = new Issue
        {
            Id = 10, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Resolved, Priority = Priority.Low,
            OccurrenceCount = 5,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.PriorityChanges.Any(pc => pc.IssueId == 10 && pc.OldPriority == Priority.Low && pc.NewPriority == Priority.Medium)));
    }

    [Fact]
    public async Task BulkDigest_HighVolume_PriorityElevatedToHigh()
    {
        var issue = new Issue
        {
            Id = 20, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low,
            OccurrenceCount = 1001, // above threshold
            FirstSeen = _now.AddDays(-1), LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var context = DefaultContext();
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(context, events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.PriorityChanges.Any(pc => pc.IssueId == 20 && pc.NewPriority == Priority.High)));
    }

    [Fact]
    public async Task BulkDigest_BucketAggregation_GroupsByHour()
    {
        var issue = new Issue
        {
            Id = 30, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Medium,
            OccurrenceCount = 1,
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var hour1 = new DateTime(2025, 6, 1, 10, 15, 0, DateTimeKind.Utc);
        var hour2 = new DateTime(2025, 6, 1, 10, 45, 0, DateTimeKind.Utc);
        var hour3 = new DateTime(2025, 6, 1, 11, 5, 0, DateTimeKind.Utc);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = hour1, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
            new() { EventId = "evt-2", Fingerprint = "fp1", Timestamp = hour2, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
            new() { EventId = "evt-3", Fingerprint = "fp1", Timestamp = hour3, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.BucketIncrements.Count == 2 &&
            w.BucketIncrements.Any(b => b.BucketStart == new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc) && b.Count == 2) &&
            w.BucketIncrements.Any(b => b.BucketStart == new DateTime(2025, 6, 1, 11, 0, 0, DateTimeKind.Utc) && b.Count == 1)));
    }

    [Fact]
    public async Task BulkDigest_ResolvedInFutureWithSameRelease_NotRegression()
    {
        var release = new Release { Id = 5, RawName = "v1.0", ProjectId = _project.Id };
        var issue = new Issue
        {
            Id = 50, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.ResolvedInFuture, Priority = Priority.Low,
            ResolvedInReleaseId = 5,
            OccurrenceCount = 3,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        _releaseService.BulkGetOrCreateReleasesAsync(Arg.Any<int>(), Arg.Any<List<ParsedEvent>>())
            .Returns([release]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Release = "v1.0", Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            !w.RegressionIssueIds.Contains(50)));
    }

    [Fact]
    public async Task BulkDigest_ResolvedInFutureWithDifferentRelease_IsRegression()
    {
        var releaseOld = new Release { Id = 5, RawName = "v1.0", ProjectId = _project.Id };
        var releaseNew = new Release { Id = 6, RawName = "v2.0", ProjectId = _project.Id };
        var issue = new Issue
        {
            Id = 51, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.ResolvedInFuture, Priority = Priority.Low,
            ResolvedInReleaseId = 5,
            OccurrenceCount = 3,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        _releaseService.BulkGetOrCreateReleasesAsync(Arg.Any<int>(), Arg.Any<List<ParsedEvent>>())
            .Returns([releaseOld, releaseNew]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Release = "v2.0", Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.RegressionIssueIds.Contains(51)));
    }

    [Fact]
    public async Task BulkDigest_NullProject_ThrowsArgumentNullException()
    {
        _projectAccess.GetProjectByIdAsync(Arg.Any<int>()).Returns((Project?)null);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        var act = () => CreateService().BulkDigestAsync(DefaultContext(), events);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BulkDigest_EmptyInboundFilters_FilterEngineNotCalled()
    {
        var issue = new Issue
        {
            Id = 1, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low, OccurrenceCount = 1,
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _eventFilterEngine.DidNotReceive().ShouldRejectEvent(Arg.Any<ParsedEvent>(), Arg.Any<List<EventFilter>>());
    }

    [Fact]
    public async Task BulkDigest_SaveEventsAsync_IsCalled()
    {
        var issue = new Issue
        {
            Id = 1, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low, OccurrenceCount = 1,
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        await _eventIngestion.Received(1).SaveEventsAsync();
    }

    [Fact]
    public async Task BulkDigest_PartiallyDifferentRelease_IsRegression()
    {
        // Any() vs All(): one event with wrong release is enough to trigger regression
        var releaseOld = new Release { Id = 5, RawName = "v1.0", ProjectId = _project.Id };
        var releaseNew = new Release { Id = 6, RawName = "v2.0", ProjectId = _project.Id };
        var issue = new Issue
        {
            Id = 52, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.ResolvedInFuture, Priority = Priority.Low,
            ResolvedInReleaseId = 5,
            OccurrenceCount = 3,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        _releaseService.BulkGetOrCreateReleasesAsync(Arg.Any<int>(), Arg.Any<List<ParsedEvent>>())
            .Returns([releaseOld, releaseNew]);
        var events = new List<ParsedEvent>
        {
            // one event on the resolved release (same) ...
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Release = "v1.0", Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
            // ... and one on a different release (regression trigger)
            new() { EventId = "evt-2", Fingerprint = "fp1", Timestamp = _now, Release = "v2.0", Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.RegressionIssueIds.Contains(52)));
    }

    [Fact]
    public async Task BulkDigest_RegressionIssue_SystemTagsApplied()
    {
        // ApplySystemTags must add Regression and Reopened tags to the issue object
        var issue = new Issue
        {
            Id = 10, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Resolved, Priority = Priority.Low,
            OccurrenceCount = 5,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        // System tags for regression/reopened (TagValueId 1000/1001 per SetupDefaults ordering)
        issue.Tags.Should().Contain(t => t.TagValueId == 1000); // sigil.regression
        issue.Tags.Should().Contain(t => t.TagValueId == 1001); // sigil.reopened
    }

    [Fact]
    public async Task BulkDigest_AtHighVolumeThreshold_NoHighVolumeTag()
    {
        // OccurrenceCount == threshold: > means no tag; >= would incorrectly apply tag
        var issue = new Issue
        {
            Id = 20, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low,
            OccurrenceCount = 1000, // exactly at threshold, not above
            FirstSeen = _now.AddDays(-1), LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        issue.Tags.Should().NotContain(t => t.TagValueId == 1002); // sigil.high-volume not applied at threshold
    }

    [Fact]
    public async Task BulkDigest_RegressionPriorityChange_HasRegressionReason()
    {
        // PriorityChange reason string must be "Regression detected" (not "")
        var issue = new Issue
        {
            Id = 10, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Resolved, Priority = Priority.Low,
            OccurrenceCount = 5,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.PriorityChanges.Any(pc => pc.IssueId == 10 && pc.Reason == "Regression detected")));
    }

    [Fact]
    public async Task BulkDigest_HighVolumeAlreadyHighPriority_PriorityNotChanged()
    {
        // AND condition: high-volume only elevates when priority < High;
        // OR mutation would elevate even when already at High
        var issue = new Issue
        {
            Id = 20, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.High,
            OccurrenceCount = 1001,
            FirstSeen = _now.AddDays(-1), LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            !w.PriorityChanges.Any(pc => pc.IssueId == 20)));
    }

    [Fact]
    public async Task BulkDigest_HighVolumePriorityChange_HasHighVolumeReason()
    {
        // PriorityChange reason string must be "High-volume threshold exceeded" (not "")
        var issue = new Issue
        {
            Id = 20, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low,
            OccurrenceCount = 1001,
            FirstSeen = _now.AddDays(-1), LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.PriorityChanges.Any(pc => pc.IssueId == 20 && pc.Reason == "High-volume threshold exceeded")));
    }

    [Fact]
    public async Task BulkDigest_AllDuplicates_WithFilters_FilterEngineNotCalled()
    {
        // After duplicate removal, parsedEvents is empty — must return before reaching filter engine
        SetupDefaults();
        _eventIngestion.FindExistingEventIdsAsync(Arg.Any<IEnumerable<string>>()).Returns(["evt-1"]);
        var context = new EventParsingContext
        {
            ProjectId = _project.Id,
            NormalizationRules = [], AutoTagRules = [],
            InboundFilters = [new EventFilter { Id = 1, ProjectId = _project.Id, Field = "level", Operator = FilterOperator.Equals, Value = "info", Enabled = true }],
            StackTraceFilters = [],
            HighVolumeThreshold = 1000,
        };
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(context, events);

        _eventFilterEngine.DidNotReceive().ShouldRejectEvent(Arg.Any<ParsedEvent>(), Arg.Any<List<EventFilter>>());
    }

    [Fact]
    public async Task BulkDigest_EventWithUser_UserPassedToUserService()
    {
        // Non-null User must be included in parsedEventUsers passed to BulkGetOrCreateEventUsersAsync
        var user = new ParsedEventUser { UniqueIdentifier = "user-1" };
        var issue = new Issue
        {
            Id = 1, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low, OccurrenceCount = 1,
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}", User = user },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        await _eventUserService.Received(1).BulkGetOrCreateEventUsersAsync(
            Arg.Is<IReadOnlyCollection<ParsedEventUser>>(c => c.Contains(user)));
    }

    [Fact]
    public async Task BulkDigest_UpdateIssueWithParsedEvents_OccurrenceCountAffectsHighVolumeTag()
    {
        // UpdateIssueWithParsedEvents increments OccurrenceCount; without it HighVolume tag wouldn't apply
        var issue = new Issue
        {
            Id = 1, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low,
            OccurrenceCount = 0, // starts at 0
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var context = new EventParsingContext
        {
            ProjectId = _project.Id,
            NormalizationRules = [], AutoTagRules = [], InboundFilters = [], StackTraceFilters = [],
            HighVolumeThreshold = 0, // any positive OccurrenceCount exceeds threshold
        };
        var events = new List<ParsedEvent>
        {
            // Tags must be non-null so UpdateIssueWithParsedEvents doesn't skip OccurrenceCount++
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}", Tags = new Dictionary<string, string>() },
        };

        await CreateService().BulkDigestAsync(context, events);

        // OccurrenceCount became 1 (> 0) → HighVolume tag applied (TagValueId=1002)
        issue.Tags.Should().Contain(t => t.TagValueId == 1002);
    }

    [Fact]
    public async Task BulkDigest_ExistingSuggestedEvent_PreservedThroughUnion()
    {
        // Union must include existing SuggestedEvent; Intersect or conditional(false) would lose it
        var existingEvent = new CapturedEvent { EventId = "existing", Timestamp = _now, ReceivedAt = _now, RawCompressedJson = null };
        var issue = new Issue
        {
            Id = 1, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low, OccurrenceCount = 1,
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            SuggestedEvent = existingEvent,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        // BulkCreateEventsEntities returns [] so only existing event flows through
        _eventRanker.GetMostRelevantEvent(Arg.Any<IEnumerable<CapturedEvent>>())
            .Returns(ci => ci.Arg<IEnumerable<CapturedEvent>>().Single()); // Single() throws if 0 or 2+ elements
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        issue.SuggestedEvent.Should().BeSameAs(existingEvent);
    }

    [Fact]
    public async Task BulkDigest_NewIssueWithNoSuggestedEvent_SuggestedEventSetFromNewEvents()
    {
        // When existing SuggestedEvent is null, conditional must NOT include [null] in the union
        var newEvent = new CapturedEvent { EventId = "new-evt", Timestamp = _now, ReceivedAt = _now, RawCompressedJson = null };
        var issue = new Issue
        {
            Id = 1, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low, OccurrenceCount = 0,
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            SuggestedEvent = null, // no pre-existing event
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        _eventIngestion.BulkCreateEventsEntities(Arg.Any<IEnumerable<ParsedEvent>>(), Arg.Any<Project>(), Arg.Any<Issue>(),
            Arg.Any<Dictionary<string, Release>>(), Arg.Any<Dictionary<string, EventUser>>(),
            Arg.Any<Dictionary<string, Dictionary<string, int>>>()).Returns([newEvent]);
        _eventRanker.GetMostRelevantEvent(Arg.Any<IEnumerable<CapturedEvent>>())
            .Returns(ci => ci.Arg<IEnumerable<CapturedEvent>>().Single()); // Single() throws if null sneaks in
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        issue.SuggestedEvent.Should().BeSameAs(newEvent);
    }

    [Fact]
    public async Task BulkDigest_HighVolumeIssue_HighVolumeTagApplied()
    {
        // OccurrenceCount > threshold → HighVolume system tag applied; < mutation would suppress it
        var issue = new Issue
        {
            Id = 20, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low,
            OccurrenceCount = 1001,
            FirstSeen = _now.AddDays(-1), LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        issue.Tags.Should().Contain(t => t.TagValueId == 1002); // sigil.high-volume tag applied
    }

    [Fact]
    public async Task BulkDigest_RegressionIssueAlreadyMediumPriority_NoPriorityChange()
    {
        // Priority < Medium: Medium issues must not produce a Medium→Medium no-op change
        var issue = new Issue
        {
            Id = 10, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Resolved, Priority = Priority.Medium,
            OccurrenceCount = 5,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.RegressionIssueIds.Contains(10) &&
            !w.PriorityChanges.Any(pc => pc.IssueId == 10)));
    }

    [Fact]
    public async Task BulkDigest_AtHighVolumeThreshold_NoPriorityElevation()
    {
        // OccurrenceCount == threshold: > means no elevation; >= would incorrectly elevate
        var issue = new Issue
        {
            Id = 20, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low,
            OccurrenceCount = 1000,
            FirstSeen = _now.AddDays(-1), LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            !w.PriorityChanges.Any(pc => pc.IssueId == 20)));
    }

    [Fact]
    public async Task BulkDigest_EventLevelBelowIssueLevel_LevelNotDowngraded()
    {
        // !(IsAbove) mutation: level must only be updated when event level is strictly above issue level
        var issue = new Issue
        {
            Id = 1, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low, OccurrenceCount = 1,
            Level = Severity.Error,
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            // Tags must be non-null so the level-update code path executes (Tags==null causes continue)
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Warning, RawJson = "{}", Tags = new Dictionary<string, string>() },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        issue.Level.Should().Be(Severity.Error);
    }

    [Fact]
    public async Task BulkDigest_EventLevelAboveIssueLevel_LevelUpgraded()
    {
        var issue = new Issue
        {
            Id = 1, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low, OccurrenceCount = 1,
            Level = Severity.Warning,
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}", Tags = new Dictionary<string, string>() },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        issue.Level.Should().Be(Severity.Error);
    }

    [Fact]
    public async Task BulkDigest_ResolvedInFutureWithNoReleaseId_IsRegression()
    {
        // IsReleaseRegression: !ResolvedInReleaseId.HasValue → return true immediately
        var issue = new Issue
        {
            Id = 60, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.ResolvedInFuture, Priority = Priority.Low,
            ResolvedInReleaseId = null,
            OccurrenceCount = 3,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.RegressionIssueIds.Contains(60)));
    }

    [Fact]
    public async Task BulkDigest_ResolvedInFutureEventWithNullRelease_IsRegression()
    {
        // IsReleaseRegression: event with null Release → always counts as regression
        var release = new Release { Id = 5, RawName = "v1.0", ProjectId = _project.Id };
        var issue = new Issue
        {
            Id = 61, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.ResolvedInFuture, Priority = Priority.Low,
            ResolvedInReleaseId = 5,
            OccurrenceCount = 3,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        _releaseService.BulkGetOrCreateReleasesAsync(Arg.Any<int>(), Arg.Any<List<ParsedEvent>>())
            .Returns([release]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Release = null, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.RegressionIssueIds.Contains(61)));
    }

    [Fact]
    public async Task BulkDigest_EventWithTags_TagAddedToIssue()
    {
        // UpdateIssueTag: new IssueTag entry added when tag doesn't already exist on issue
        var issue = new Issue
        {
            Id = 1, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low, OccurrenceCount = 1,
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}",
                    Tags = new Dictionary<string, string> { ["browser"] = "Chrome" } },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        // browser:Chrome → TagValueId 2000 (first non-system tag in SetupDefaults)
        issue.Tags.Should().Contain(t => t.TagValueId == 2000 && t.OccurrenceCount == 1);
    }

    [Fact]
    public async Task BulkDigest_EventWithExistingTag_TagOccurrenceCountIncremented()
    {
        // UpdateIssueTag else-path: issueTag.OccurrenceCount++ when tag already exists on issue
        var existingTag = new IssueTag { TagValueId = 2000, OccurrenceCount = 5, FirstSeen = _now.AddDays(-2), LastSeen = _now.AddDays(-1) };
        var issue = new Issue
        {
            Id = 1, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Open, Priority = Priority.Low, OccurrenceCount = 1,
            FirstSeen = _now, LastSeen = _now, LastChangedAt = _now,
            Tags = new List<IssueTag> { existingTag },
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}",
                    Tags = new Dictionary<string, string> { ["browser"] = "Chrome" } },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        existingTag.OccurrenceCount.Should().Be(6);
    }

    [Fact]
    public async Task BulkDigest_RegressionTagAlreadyExists_SystemTagOccurrenceCountIncremented()
    {
        // AddSystemTag else-path: existing.OccurrenceCount++ when system tag already present on issue
        var existingRegressionTag = new IssueTag { TagValueId = 1000, OccurrenceCount = 2, FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1) };
        var issue = new Issue
        {
            Id = 10, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Resolved, Priority = Priority.Low,
            OccurrenceCount = 5,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag> { existingRegressionTag },
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        existingRegressionTag.OccurrenceCount.Should().Be(3);
    }

    [Fact]
    public async Task BulkDigest_RegressionAndHighVolumeOnSameIssue_OnlyOnePriorityChangeEmitted()
    {
        // ElevateIssuePriorities: guard prevents duplicate PriorityChange for same issue
        // when both regression and high-volume conditions apply simultaneously
        var issue = new Issue
        {
            Id = 10, Fingerprint = "fp1", ProjectId = _project.Id,
            Status = IssueStatus.Resolved, Priority = Priority.Low,
            OccurrenceCount = 1001,
            FirstSeen = _now.AddDays(-1), LastSeen = _now.AddDays(-1), LastChangedAt = _now.AddDays(-1),
            Tags = new List<IssueTag>(),
        };
        SetupDefaults([issue]);
        var events = new List<ParsedEvent>
        {
            new() { EventId = "evt-1", Fingerprint = "fp1", Timestamp = _now, Platform = Platform.CSharp, Level = Severity.Error, RawJson = "{}" },
        };

        await CreateService().BulkDigestAsync(DefaultContext(), events);

        _postDigestionQueue.Received(1).TryEnqueue(Arg.Is<PostDigestionWork>(w =>
            w.PriorityChanges.Count(pc => pc.IssueId == 10) == 1));
    }
}
