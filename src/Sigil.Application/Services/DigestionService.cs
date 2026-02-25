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
    public async Task BulkDigestAsync(EventParsingContext context, List<ParsedEvent> parsedEvents, CancellationToken ct = default)
    {
        var project = await projectService.GetProjectByIdAsync(context.ProjectId);
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

        List<KeyValuePair<string, string>> requiredTags = parsedEvents.Where(item => item.Tags != null).SelectMany(item => item.Tags!).Distinct().ToList();
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

        foreach (IGrouping<string, ParsedEvent> group in issueGrouping)
        {
            Issue issue = issues[group.Key];

            UpdateIssueWithParsedEvents(group, tagValues, issue);

            List<CapturedEvent> eventsEntities = eventService.BulkCreateEventsEntities(group, project, issue, releases, eventUsers, tagValues);
            issue.SuggestedEvent = eventRanker.GetMostRelevantEvent(eventsEntities.Union(issue.SuggestedEvent != null ? [issue.SuggestedEvent] : []));
        }

        await eventService.SaveEventsAsync();

        postDigestionQueue.TryEnqueue(new PostDigestionWork(
            context.ProjectId,
            issues.Values.Select(i => i.Id).ToList(),
            newIssueIds,
            regressionIssueIds));
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

}
