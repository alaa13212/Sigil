using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Domain;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Services;

public class DigestionService(
    IProjectService projectService,
    IIssueService issueService,
    IEventService eventService,
    IReleaseService releaseService,
    IEventUserService eventUserService,
    ITagService tagService,
    IEventRanker eventRanker,
    IEventFilterService eventFilterService,
    IWorker<PostDigestionWork> postDigestionQueue,
    IDateTime dateTime
) : IDigestionService
{
    public async Task BulkDigestAsync(EventParsingContext context, List<ParsedEvent> parsedEvents, CancellationToken ct = default)
    {
        Project? project = await projectService.GetProjectByIdAsync(context.ProjectId);
        ArgumentNullException.ThrowIfNull(project);

        var existingIds = await eventService.FindExistingEventIdsAsync(parsedEvents.Select(e => e.EventId));
        parsedEvents.RemoveAll(e => existingIds.Contains(e.EventId));
        if (parsedEvents.Count == 0)
            return;

        if (context.InboundFilters.Count > 0)
            parsedEvents.RemoveAll(e => eventFilterService.ShouldRejectEvent(e, context.InboundFilters));
        if (parsedEvents.Count == 0)
            return;

        Dictionary<string, Release> releases = (await releaseService.BulkGetOrCreateReleasesAsync(context.ProjectId, parsedEvents)).ToDictionary(r => r.RawName);

        List<KeyValuePair<string, string>> userTagPairs = parsedEvents.Where(item => item.Tags != null).SelectMany(item => item.Tags!).Distinct().ToList();
        List<KeyValuePair<string, string>> systemTagPairs = SystemTags.AllPairs;
        
        List<KeyValuePair<string, string>> requiredTags = [.. userTagPairs, .. systemTagPairs];
        Dictionary<string, Dictionary<string, int>> tagValues = (await tagService.BulkGetOrCreateTagsAsync(requiredTags))
            .ToLookup(t => t.TagKey!.Key)
            .ToDictionary(g => g.Key!, g => g.ToDictionary(tv => tv.Value, tv => tv.Id));

        List<ParsedEventUser> parsedEventUsers = parsedEvents.Select(item => item.User).Where(u => u != null).ToList()!;
        Dictionary<string, EventUser> eventUsers = (await eventUserService.BulkGetOrCreateEventUsersAsync(parsedEventUsers)).ToDictionary(u => u.UniqueIdentifier);

        ILookup<string, ParsedEvent> issueGrouping = parsedEvents
            .Where(e => !e.Fingerprint.IsNullOrEmpty())
            .ToLookup(e => e.Fingerprint!);
        Dictionary<string, Issue> issues = (await issueService.BulkGetOrCreateIssuesAsync(project, issueGrouping)).ToDictionary(i => i.Fingerprint);

        // Track alert signals before mutation
        HashSet<int> newIssueIds = issues.Values.Where(i => i.OccurrenceCount == 0).Select(i => i.Id).ToHashSet();
        HashSet<int> regressionIssueIds = issues.Values
            .Where(i => i.Status == IssueStatus.Resolved ||
                        (i.Status == IssueStatus.ResolvedInFuture && IsReleaseRegression(i, issueGrouping[i.Fingerprint], releases)))
            .Select(i => i.Id)
            .ToHashSet();

        DateTime now = dateTime.UtcNow;

        foreach (IGrouping<string, ParsedEvent> group in issueGrouping)
        {
            Issue issue = issues[group.Key];

            UpdateIssueWithParsedEvents(group, tagValues, issue);

            List<CapturedEvent> eventsEntities = eventService.BulkCreateEventsEntities(group, project, issue, releases, eventUsers, tagValues);
            issue.SuggestedEvent = eventRanker.GetMostRelevantEvent(eventsEntities.Union(issue.SuggestedEvent != null ? [issue.SuggestedEvent] : []));
        }

        ApplySystemTags(context, regressionIssueIds, issues, tagValues, now);

        List<PriorityChange> priorityChanges = ElevateIssuePriorities(context, regressionIssueIds, issues);

        await eventService.SaveEventsAsync();

        postDigestionQueue.TryEnqueue(new PostDigestionWork(
            context.ProjectId,
            issues.Values.Select(i => i.Id).ToList(),
            newIssueIds,
            regressionIssueIds,
            AggregateEventCountsByBucket(parsedEvents, issues),
            priorityChanges));
    }

    private static bool IsReleaseRegression(Issue issue, IEnumerable<ParsedEvent> events, Dictionary<string, Release> releases)
    {
        // No release recorded when resolved → any event is a regression
        if (!issue.ResolvedInReleaseId.HasValue) return true;
        // Any event with a different (or missing) release counts as a regression
        return events.Any(e =>
        {
            if (e.Release == null) return true;
            return releases.GetValueOrDefault(e.Release)?.Id != issue.ResolvedInReleaseId;
        });
    }

    private static void UpdateIssueWithParsedEvents(IGrouping<string, ParsedEvent> group, Dictionary<string, Dictionary<string, int>> tagValues, Issue issue)
    {
        foreach (ParsedEvent parsedEvent in group)
        {
            if (parsedEvent.Tags == null)
                continue;

            foreach (KeyValuePair<string, string> tag in parsedEvent.Tags)
            {
                UpdateIssueTag(tagValues, tag, issue, parsedEvent);
            }

            issue.OccurrenceCount++;
            issue.FirstSeen = TimeMath.Earlier(issue.FirstSeen, parsedEvent.Timestamp);
            issue.LastSeen = TimeMath.Later(issue.LastSeen, parsedEvent.Timestamp);
        }
    }

    private static void AddSystemTag(Issue issue, string tagKey, Dictionary<string, Dictionary<string, int>> tagValues, DateTime timestamp)
    {
        if (!tagValues.TryGetValue(tagKey, out var valueMap)) return;
        if (!valueMap.TryGetValue("true", out var tagValueId)) return;

        IssueTag? existing = issue.Tags.FirstOrDefault(t => t.TagValueId == tagValueId);
        if (existing is null)
        {
            issue.Tags.Add(new IssueTag
            {
                Issue = issue,
                TagValueId = tagValueId,
                OccurrenceCount = 1,
                FirstSeen = timestamp,
                LastSeen = timestamp,
            });
            issue.LastChangedAt = TimeMath.Later(issue.LastChangedAt, timestamp);
        }
        else
        {
            existing.OccurrenceCount++;
            existing.LastSeen = TimeMath.Later(existing.LastSeen, timestamp);
        }
    }

    private static void UpdateIssueTag(Dictionary<string, Dictionary<string, int>> tagValues, KeyValuePair<string, string> tag, Issue issue, ParsedEvent parsedEvent)
    {
        int tagValueId = tagValues[tag.Key][tag.Value];
        IssueTag? issueTag = issue.Tags.FirstOrDefault(iTag => iTag.TagValueId == tagValueId);
        if (issueTag == null)
        {
            issueTag = new IssueTag { Issue = issue, TagValueId = tagValueId, FirstSeen = parsedEvent.Timestamp, LastSeen = parsedEvent.Timestamp };
            issue.Tags.Add(issueTag);
        }

        issueTag.OccurrenceCount++;
        issueTag.FirstSeen = TimeMath.Earlier(issueTag.FirstSeen, parsedEvent.Timestamp);
        issueTag.LastSeen = TimeMath.Later(issueTag.LastSeen, parsedEvent.Timestamp);
    }
    
    

    private static List<EventBucketIncrement> AggregateEventCountsByBucket(List<ParsedEvent> parsedEvents, Dictionary<string, Issue> issues)
    {
        List<EventBucketIncrement> bucketIncrements = parsedEvents
            .Where(e => !e.Fingerprint.IsNullOrEmpty() && issues.ContainsKey(e.Fingerprint!))
            .GroupBy(e => new
            {
                IssueId = issues[e.Fingerprint!].Id,
                BucketStart = new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0, DateTimeKind.Utc)
            })
            .Select(g => new EventBucketIncrement(g.Key.IssueId, g.Key.BucketStart, g.Count()))
            .ToList();
        return bucketIncrements;
    }
    

    private static void ApplySystemTags(EventParsingContext context, HashSet<int> regressionIssueIds, Dictionary<string, Issue> issues,
        Dictionary<string, Dictionary<string, int>> tagValues, DateTime now)
    {
        foreach (var issueId in regressionIssueIds)
        {
            var issue = issues.Values.First(i => i.Id == issueId);
            AddSystemTag(issue, SystemTags.Regression, tagValues, now);
            AddSystemTag(issue, SystemTags.Reopened, tagValues, now);
        }

        foreach (var issue in issues.Values)
        {
            if (issue.OccurrenceCount > context.HighVolumeThreshold)
                AddSystemTag(issue, SystemTags.HighVolume, tagValues, now);
        }
    }
    
    

    private static List<PriorityChange> ElevateIssuePriorities(EventParsingContext context, HashSet<int> regressionIssueIds, Dictionary<string, Issue> issues)
    {
        var priorityChanges = new List<PriorityChange>();
        foreach (var issueId in regressionIssueIds)
        {
            var issue = issues.Values.First(i => i.Id == issueId);
            if (issue.Priority < Priority.Medium)
            {
                priorityChanges.Add(new PriorityChange(issue.Id, issue.Priority, Priority.Medium, "Regression detected"));
                issue.Priority = Priority.Medium;
            }
        }
        foreach (var issue in issues.Values)
        {
            if (issue.OccurrenceCount > context.HighVolumeThreshold && issue.Priority < Priority.High)
            {
                if (priorityChanges.All(pc => pc.IssueId != issue.Id))
                    priorityChanges.Add(new PriorityChange(issue.Id, issue.Priority, Priority.High, "High-volume threshold exceeded"));
                issue.Priority = Priority.High;
            }
        }

        return priorityChanges;
    }

}
