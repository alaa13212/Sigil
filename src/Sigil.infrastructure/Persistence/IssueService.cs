using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.infrastructure.Persistence;

internal class IssueService(SigilDbContext dbContext, IEventRanker eventRanker, IDateTime dateTime) : IIssueService
{
    public async Task<List<Issue>> BulkGetOrCreateIssuesAsync(Project project, IEnumerable<IGrouping<string, ParsedEvent>> eventsByFingerprint)
    {
        List<IGrouping<string, ParsedEvent>> groupings = eventsByFingerprint.ToList();
        List<string> fingerprints = groupings.Select(g => g.Key).ToList();

        List<Issue> results = [];

        // Get existing issues
        results.AddRange(
            await dbContext.Issues
                .AsTracking()
                .Include(i => i.Tags)
                    .ThenInclude(it => it.TagValue)
                    .ThenInclude(tv => tv!.TagKey)
                .Where(i => i.ProjectId == project.Id && fingerprints.Contains(i.Fingerprint))
                .ToListAsync());

        // Find fingerprints that need new issues
        List<string> existingFingerprints = results.Select(i => i.Fingerprint).ToList();
        List<string> newFingerprints = fingerprints.Except(existingFingerprints).ToList();

        if (newFingerprints.Any())
        {
            // Create new issues for missing fingerprints
            var newIssues = new List<Issue>();
            foreach (var fingerprint in newFingerprints)
            {
                var eventsForFingerprint = groupings.First(g => g.Key == fingerprint);
                var representativeEvent = eventRanker.GetMostRelevantEvent(eventsForFingerprint);

                var issue = new Issue
                {
                    ProjectId = project.Id,
                    Title = representativeEvent.NormalizedMessage ?? "Unknown Error",
                    ExceptionType = representativeEvent.ExceptionType,
                    Level = representativeEvent.Level,
                    Priority = Priority.Low,
                    Status = IssueStatus.Open,
                    Fingerprint = fingerprint,
                    FirstSeen = dateTime.UtcNow,
                    LastSeen = dateTime.UtcNow,
                    OccurrenceCount = 0,
                    Culprit = representativeEvent.Culprit,
                };

                newIssues.Add(issue);
            }

            dbContext.Issues.AddRange(newIssues);
            await dbContext.SaveChangesAsync();
            results.AddRange(newIssues);
        }

        return results;
    }
}
