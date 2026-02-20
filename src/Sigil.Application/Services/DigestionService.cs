using Sigil.Application.Interfaces;
using Sigil.Application.Models;
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
    IWorker<PostDigestionWork> postDigestionQueue
) : IDigestionService
{
    public async Task BulkDigestAsync(int projectId, List<ParsedEvent> parsedEvents, CancellationToken ct = default)
    {
        var project = await projectService.GetProjectByIdAsync(projectId);
        ArgumentNullException.ThrowIfNull(project);

        var existingIds = await eventService.FindExistingEventIdsAsync(parsedEvents.Select(e => e.EventId));
        parsedEvents.RemoveAll(e => existingIds.Contains(e.EventId));
        if (parsedEvents.Count == 0)
            return;

        List<EventFilter> filters = await eventFilterService.GetRawFiltersForProjectAsync(projectId);
        if (filters.Count > 0)
            parsedEvents.RemoveAll(e => eventFilterService.ShouldRejectEvent(e, filters));
        if (parsedEvents.Count == 0)
            return;

        List<string> requiredReleases = parsedEvents.Select(item => item.Release).Distinct().ToList();
        Dictionary<string, Release> releases = (await releaseService.BulkGetOrCreateReleasesAsync(projectId, requiredReleases)).ToDictionary(r => r.RawName);

        List<KeyValuePair<string, string>> requiredTags = parsedEvents.Where(item => item.Tags != null).SelectMany(item => item.Tags!).Distinct().ToList();
        Dictionary<string, TagValue> tagValues = (await tagService.BulkGetOrCreateTagsAsync(requiredTags))
            .ToDictionary(t => t.TagKey is not null
                ? $"{t.TagKey.Key}:{t.Value}"
                : throw new InvalidOperationException($"TagValue {t.Id} has a null TagKey"));

        List<ParsedEventUser> parsedEventUsers = parsedEvents.Select(item => item.User).Where(u => u != null).ToList()!;
        Dictionary<string, EventUser> eventUsers = (await eventUserService.BulkGetOrCreateEventUsersAsync(parsedEventUsers)).ToDictionary(u => u.UniqueIdentifier);

        ILookup<string, ParsedEvent> issueGrouping = parsedEvents
            .Where(e => !e.Fingerprint.IsNullOrEmpty())
            .ToLookup(e => e.Fingerprint!);
        Dictionary<string, Issue> issues = (await issueService.BulkGetOrCreateIssuesAsync(project, issueGrouping)).ToDictionary(i => i.Fingerprint);

        // Track alert signals before mutation
        HashSet<int> newIssueIds = issues.Values.Where(i => i.OccurrenceCount == 0).Select(i => i.Id).ToHashSet();
        HashSet<int> regressionIssueIds = issues.Values.Where(i => i.Status == IssueStatus.Resolved).Select(i => i.Id).ToHashSet();

        List<CapturedEvent> events = [];
        foreach (IGrouping<string, ParsedEvent> group in issueGrouping)
        {
            Issue issue = issues[group.Key];

            UpdateIssueWithParsedEvents(group, tagValues, issue);

            List<CapturedEvent> eventsEntities = eventService.BulkCreateEventsEntities(group, project, issue, releases, eventUsers, tagValues).ToList();
            events.AddRange(eventsEntities);

            issue.SuggestedEvent = eventRanker.GetMostRelevantEvent(eventsEntities.Union(issue.SuggestedEvent != null ? [issue.SuggestedEvent] : []));
        }

        await eventService.SaveEventsAsync(events);

        postDigestionQueue.TryEnqueue(new PostDigestionWork(
            projectId,
            issues.Values.Select(i => i.Id).ToList(),
            newIssueIds,
            regressionIssueIds));
    }

    private static void UpdateIssueWithParsedEvents(IGrouping<string, ParsedEvent> group, Dictionary<string, TagValue> tagValues, Issue issue)
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

    private static void UpdateIssueTag(Dictionary<string, TagValue> tagValues, KeyValuePair<string, string> tag, Issue issue, ParsedEvent parsedEvent)
    {
        TagValue tagValue = tagValues[$"{tag.Key}:{tag.Value}"];
        IssueTag? issueTag = issue.Tags.FirstOrDefault(iTag => iTag.TagValueId == tagValue.Id);
        if (issueTag == null)
        {
            issueTag = new IssueTag { Issue = issue, TagValueId = tagValues[$"{tag.Key}:{tag.Value}"].Id, FirstSeen = parsedEvent.Timestamp, LastSeen = parsedEvent.Timestamp };
            issue.Tags.Add(issueTag);
        }

        issueTag.OccurrenceCount++;
        issueTag.FirstSeen = TimeMath.Earlier(issueTag.FirstSeen, parsedEvent.Timestamp);
        issueTag.LastSeen = TimeMath.Later(issueTag.LastSeen, parsedEvent.Timestamp);
    }

}
