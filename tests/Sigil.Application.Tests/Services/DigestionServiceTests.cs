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
}
