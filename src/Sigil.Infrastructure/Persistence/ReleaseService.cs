using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Infrastructure.Persistence;

internal partial class ReleaseService(SigilDbContext dbContext, IReleaseCache releaseCache) : IReleaseService
{
    public async Task<List<Release>> BulkGetOrCreateReleasesAsync(int projectId, List<ParsedEvent> parsedEvents)
    {
        IEnumerable<string> rawValues = parsedEvents.Where(e => e.Release != null).Select(e => e.Release!).Distinct();
        var (results, misses) = releaseCache.TryGetMany(rawValues, 
            rawValue => releaseCache.TryGet(projectId, rawValue, out Release? cached) ? cached : null);

        if (misses.Count > 0)
        {
            var fromDb = new List<Release>();
            fromDb.AddRange(
                await dbContext.Releases
                    .Where(r => r.ProjectId == projectId && misses.Contains(r.RawName))
                    .ToListAsync());

            List<string> existingRawValues = fromDb.Select(r => r.RawName).ToList();
            List<string> newRawValues = misses.Except(existingRawValues).ToList();

            if (newRawValues.Any())
            {
                List<Release> newReleases = newRawValues.Select(rawValue => ParseAndCreateReleaseAsync(projectId, rawValue)).ToList();
                newReleases.ForEach(release => release.FirstSeenAt = parsedEvents.Where(e => e.Release == release.RawName).Min(e => e.Timestamp));
                dbContext.Releases.AddRange(newReleases);
                await dbContext.SaveChangesAsync();
                fromDb.AddRange(newReleases);

                foreach (Release newRelease in newReleases)
                    dbContext.Entry(newRelease).State = EntityState.Detached;

                // A new release has shipped — auto-transition "resolved in next release" issues to Resolved
                var newReleaseIds = newReleases.Select(r => r.Id).ToList();
                await dbContext.Issues
                    .Where(i => i.ProjectId == projectId &&
                                i.Status == IssueStatus.ResolvedInFuture &&
                                (i.ResolvedInReleaseId == null || !newReleaseIds.Contains(i.ResolvedInReleaseId.Value)))
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, IssueStatus.Resolved)
                                              .SetProperty(i => i.ResolvedInReleaseId, (int?)null));
            }

            foreach (Release release in fromDb)
                releaseCache.Set(projectId, release);

            results.AddRange(fromDb);
        }

        return results;
    }

    private Release ParseAndCreateReleaseAsync(int projectId, string rawValue)
    {
        (string? package, string? version, int? build, string? commitSha) = ParseReleaseComponents(rawValue);

        var release = new Release
        {
            ProjectId = projectId,
            RawName = rawValue,
            Package = package,
            SemanticVersion = version,
            Build = build,
            CommitSha = commitSha,
        };

        dbContext.Releases.Add(release);
        return release;
    }


    private static (string? package, string? version, int? build, string? commitSha) ParseReleaseComponents(string rawValue)
    {
        string? package = null;
        string? version = null;
        int? build = null;
        string? commitSha = null;

        var packageMatch = PackageRegex().Match(rawValue);
        if (packageMatch.Success)
        {
            package = packageMatch.Groups["package"].Value;
        }

        var semVerMatch = SemVerRegex().Match(rawValue);
        if (semVerMatch.Success)
        {
            version = semVerMatch.Value;
        }

        var buildMatch = BuildNumberRegex().Match(rawValue);
        if (buildMatch.Success && int.TryParse(buildMatch.Groups["build"].Value, out var buildNum))
        {
            build = buildNum;
        }

        var commitMatch = CommitShaRegex().Match(rawValue);
        if (commitMatch.Success)
        {
            commitSha = commitMatch.Value;
        }

        return (package, version, build, commitSha);
    }

    [GeneratedRegex(@"^(?<package>[^@]+)@", RegexOptions.Compiled)]
    private static partial Regex PackageRegex();

    [GeneratedRegex(@"\d+\.\d+\.\d+(?:-[\w.-]+)?(?:\+[\w.-]+)?", RegexOptions.Compiled)]
    private static partial Regex SemVerRegex();

    [GeneratedRegex(@"build[:\s]+(?<build>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex BuildNumberRegex();

    [GeneratedRegex(@"[0-9a-f]{7,40}", RegexOptions.Compiled)]
    private static partial Regex CommitShaRegex();
}
