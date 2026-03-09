using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class EventServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static ICompressionService StubCompression()
    {
        var comp = Substitute.For<ICompressionService>();
        comp.CompressString(Arg.Any<string>()).Returns(x => System.Text.Encoding.UTF8.GetBytes(x.Arg<string>()));
        comp.DecompressToString(Arg.Any<byte[]>()).Returns(x => System.Text.Encoding.UTF8.GetString(x.Arg<byte[]>()));
        return comp;
    }

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static EventService Create(SigilDbContext ctx, ICompressionService? compression = null)
        => new(ctx, compression ?? StubCompression(), StubDateTime());

    private static ParsedEvent MakeEvent(string? release = null, ParsedEventUser? user = null,
        Dictionary<string, string>? tags = null, List<ParsedStackFrame>? frames = null,
        string rawJson = "{}", DateTime? timestamp = null) => new()
    {
        EventId = Guid.NewGuid().ToString("N"),
        Timestamp = timestamp ?? DateTime.UtcNow,
        ReceivedAt = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = Severity.Error,
        RawJson = rawJson,
        Release = release,
        User = user,
        Tags = tags,
        Stacktrace = frames ?? [],
    };

    // ── FindExistingEventIdsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task FindExisting_EmptyInput_ReturnsEmpty()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var result = await service.FindExistingEventIdsAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindExisting_NoMatchingIds_ReturnsEmpty()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        var service = Create(ctx);

        var result = await service.FindExistingEventIdsAsync(["nonexistent-id"]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FindExisting_AllMatch_ReturnsAll()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var evt1 = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        var evt2 = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        var service = Create(ctx);

        var result = await service.FindExistingEventIdsAsync([evt1.EventId, evt2.EventId]);

        result.Should().BeEquivalentTo([evt1.EventId, evt2.EventId]);
    }

    [Fact]
    public async Task FindExisting_PartialMatch_ReturnsOnlyMatching()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var evt = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        var service = Create(ctx);

        var result = await service.FindExistingEventIdsAsync([evt.EventId, "missing-id"]);

        result.Should().ContainSingle().Which.Should().Be(evt.EventId);
    }

    // ── BulkCreateEventsEntities + SaveEventsAsync ───────────────────────────

    [Fact]
    public async Task BulkCreate_SimpleEvent_EntityFieldsMapped()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = Create(ctx);
        var timestamp = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var parsedEvent = MakeEvent(timestamp: timestamp);

        service.BulkCreateEventsEntities([parsedEvent], project, issue, [], [], []);
        await service.SaveEventsAsync();

        await using var verifyCtx = Ctx();
        var saved = verifyCtx.Events.FirstOrDefault(e => e.EventId == parsedEvent.EventId);
        saved.Should().NotBeNull();
        saved!.ProjectId.Should().Be(project.Id);
        saved.IssueId.Should().Be(issue.Id);
        saved.Level.Should().Be(Severity.Error);
        saved.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public async Task BulkCreate_WithRelease_ReleaseFkSet()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, "1.0.0");
        var service = Create(ctx);
        var parsedEvent = MakeEvent(release: "1.0.0");

        service.BulkCreateEventsEntities([parsedEvent], project, issue,
            releases: new Dictionary<string, Release> { ["1.0.0"] = release },
            users: [],
            tagValues: []);
        await service.SaveEventsAsync();

        await using var verifyCtx = Ctx();
        var saved = verifyCtx.Events.FirstOrDefault(e => e.EventId == parsedEvent.EventId);
        saved!.ReleaseId.Should().Be(release.Id);
    }

    [Fact]
    public async Task BulkCreate_WithUser_UserFkSet()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var eventUser = await TestHelper.CreateEventUserAsync(ctx, "user123");
        var service = Create(ctx);
        var parsedEvent = MakeEvent(user: new ParsedEventUser { UniqueIdentifier = eventUser.UniqueIdentifier });

        service.BulkCreateEventsEntities([parsedEvent], project, issue,
            releases: [],
            users: new Dictionary<string, EventUser> { [eventUser.UniqueIdentifier] = eventUser },
            tagValues: []);
        await service.SaveEventsAsync();

        await using var verifyCtx = Ctx();
        var saved = verifyCtx.Events.FirstOrDefault(e => e.EventId == parsedEvent.EventId);
        saved!.UserId.Should().Be(eventUser.UniqueIdentifier);
    }

    [Fact]
    public async Task BulkCreate_WithStackFrames_FramesPersisted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = Create(ctx);
        var frames = new List<ParsedStackFrame>
        {
            new() { Function = "MyApp.Foo.Bar", Filename = "Foo.cs", LineNumber = 42, InApp = true },
            new() { Function = "System.Threading.Thread.Start", InApp = false },
        };
        var parsedEvent = MakeEvent(frames: frames);

        service.BulkCreateEventsEntities([parsedEvent], project, issue, [], [], []);
        await service.SaveEventsAsync();

        await using var verifyCtx = Ctx();
        var saved = verifyCtx.Events
            .Where(e => e.EventId == parsedEvent.EventId)
            .Select(e => e.Id)
            .FirstOrDefault();
        var savedFrames = verifyCtx.StackFrames.Where(f => f.EventId == saved).ToList();
        savedFrames.Should().HaveCount(2);
        savedFrames.Should().ContainSingle(f => f.Function == "MyApp.Foo.Bar" && f.InApp);
    }

    [Fact]
    public async Task BulkCreate_RawJsonCompressed_StoredAsBytes()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var compression = StubCompression();
        var service = Create(ctx, compression);
        var parsedEvent = MakeEvent(rawJson: "{\"test\":true}");

        service.BulkCreateEventsEntities([parsedEvent], project, issue, [], [], []);
        await service.SaveEventsAsync();

        compression.Received().CompressString("{\"test\":true}");
    }

    [Fact]
    public async Task BulkCreate_MultipleEvents_AllPersisted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = Create(ctx);
        var events = new List<ParsedEvent> { MakeEvent(), MakeEvent(), MakeEvent() };

        service.BulkCreateEventsEntities(events, project, issue, [], [], []);
        await service.SaveEventsAsync();

        await using var verifyCtx = Ctx();
        foreach (var evt in events)
            verifyCtx.Events.Any(e => e.EventId == evt.EventId).Should().BeTrue();
    }

    // ── GetEventByIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_NotFound_ReturnsNull()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var result = await service.GetEventByIdAsync(long.MaxValue);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetById_IncludeStackFrames_FramesLoaded()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var evt = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        await TestHelper.CreateStackFrameAsync(ctx, evt.Id, "MyApp.Test", inApp: true);
        var service = Create(ctx);

        var result = await service.GetEventByIdAsync(evt.Id, includeStackFrames: true);

        result.Should().NotBeNull();
        result!.StackFrames.Should().HaveCount(1);
        result.StackFrames.First().Function.Should().Be("MyApp.Test");
    }

    [Fact]
    public async Task GetById_ExcludeStackFrames_FramesNotLoaded()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var evt = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        await TestHelper.CreateStackFrameAsync(ctx, evt.Id, "MyApp.Test");

        // Fresh context: no identity-map cache from the setup queries
        await using var ctx2 = Ctx();
        var service = Create(ctx2);

        var result = await service.GetEventByIdAsync(evt.Id, includeStackFrames: false);

        result.Should().NotBeNull();
        result!.StackFrames.Should().BeEmpty();
    }

    // ── GetEventsForIssueAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetEventsForIssue_NoEvents_ReturnsEmpty()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var service = Create(ctx);

        var (items, totalCount) = await service.GetEventsForIssueAsync(issue.Id);

        items.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetEventsForIssue_SortedByTimestampDescending()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var now = DateTime.UtcNow;
        var oldest = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddHours(-2));
        var middle = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddHours(-1));
        var newest = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now);
        var service = Create(ctx);

        var (items, _) = await service.GetEventsForIssueAsync(issue.Id);

        items[0].EventId.Should().Be(newest.EventId);
        items[1].EventId.Should().Be(middle.EventId);
        items[2].EventId.Should().Be(oldest.EventId);
    }

    [Fact]
    public async Task GetEventsForIssue_Paginated_ReturnsCorrectPage()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
            await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddMinutes(-i));
        var service = Create(ctx);

        var (page1, total) = await service.GetEventsForIssueAsync(issue.Id, page: 1, pageSize: 2);
        var (page2, _) = await service.GetEventsForIssueAsync(issue.Id, page: 2, pageSize: 2);

        total.Should().Be(5);
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page1.Select(e => e.EventId).Should().NotIntersectWith(page2.Select(e => e.EventId));
    }

    [Fact]
    public async Task GetEventsForIssue_TotalCountMatchesAllEvents()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        for (int i = 0; i < 7; i++)
            await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        var service = Create(ctx);

        var (_, totalCount) = await service.GetEventsForIssueAsync(issue.Id, page: 1, pageSize: 3);

        totalCount.Should().Be(7);
    }

    // ── GetEventSummariesAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetEventSummaries_MapsToDtos()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var now = DateTime.UtcNow;
        for (int i = 0; i < 3; i++)
            await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddMinutes(-i));
        var service = Create(ctx);

        var result = await service.GetEventSummariesAsync(issue.Id, page: 1, pageSize: 10);

        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
        result.Items.Should().AllSatisfy(s => s.Id.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task GetEventSummaries_Paginated_ReturnsCorrectSlice()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
            await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddMinutes(-i));
        var service = Create(ctx);

        var result = await service.GetEventSummariesAsync(issue.Id, page: 2, pageSize: 2);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(2);
    }

    // ── GetRawEventJsonAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetRawEventJson_NotFound_ReturnsNull()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var result = await service.GetRawEventJsonAsync(long.MaxValue);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRawEventJson_Found_ReturnsDecompressedBytes()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        var compression = StubCompression();
        var service = Create(ctx, compression);
        var parsedEvent = MakeEvent(rawJson: "{\"key\":\"value\"}");
        service.BulkCreateEventsEntities([parsedEvent], project, issue, [], [], []);
        await service.SaveEventsAsync();

        await using var ctx2 = Ctx();
        var service2 = Create(ctx2, compression);
        var evtId = ctx2.Events.First(e => e.EventId == parsedEvent.EventId).Id;

        var result = await service2.GetRawEventJsonAsync(evtId);

        result.Should().NotBeNull();
        System.Text.Encoding.UTF8.GetString(result).Should().Be("{\"key\":\"value\"}");
    }

    // ── GetAdjacentEventIdsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetAdjacentEvents_MiddleEvent_ReturnsBothNeighbors()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var now = DateTime.UtcNow;
        var oldest = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddHours(-2));
        var middle = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddHours(-1));
        var newest = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now);
        var service = Create(ctx);

        var result = await service.GetAdjacentEventIdsAsync(issue.Id, middle.Id);

        result.PreviousEventId.Should().Be(oldest.Id);
        result.NextEventId.Should().Be(newest.Id);
    }

    [Fact]
    public async Task GetAdjacentEvents_OldestEvent_NoPrevious()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var now = DateTime.UtcNow;
        var oldest = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddHours(-1));
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now);
        var service = Create(ctx);

        var result = await service.GetAdjacentEventIdsAsync(issue.Id, oldest.Id);

        result.PreviousEventId.Should().BeNull();
        result.NextEventId.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAdjacentEvents_NewestEvent_NoNext()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var now = DateTime.UtcNow;
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddHours(-1));
        var newest = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now);
        var service = Create(ctx);

        var result = await service.GetAdjacentEventIdsAsync(issue.Id, newest.Id);

        result.PreviousEventId.Should().NotBeNull();
        result.NextEventId.Should().BeNull();
    }

    [Fact]
    public async Task GetAdjacentEvents_OnlyEvent_BothNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var evt = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        var service = Create(ctx);

        var result = await service.GetAdjacentEventIdsAsync(issue.Id, evt.Id);

        result.PreviousEventId.Should().BeNull();
        result.NextEventId.Should().BeNull();
    }
}
