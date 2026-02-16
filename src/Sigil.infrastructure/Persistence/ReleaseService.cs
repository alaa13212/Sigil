using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence;

internal partial class ReleaseService(SigilDbContext dbContext, IDateTime dateTime) : IReleaseService
{
    public async Task<Release> CreateReleaseAsync(int projectId, string rawValue)
    {
        var existing = await GetReleaseByRawValueAsync(projectId, rawValue);
        if (existing != null)
        {
            throw new InvalidOperationException($"Release '{rawValue}' already exists for project '{projectId}'");
        }

        Release release = ParseAndCreateReleaseAsync(projectId, rawValue);
        await dbContext.SaveChangesAsync();
        return release;
    }

    public async Task<List<Release>> BulkGetOrCreateReleasesAsync(int projectId, IEnumerable<string> rawValues)
    {
        List<Release> results = [];

        results.AddRange(
            await dbContext.Releases
                .Where(r => r.ProjectId == projectId && rawValues.Contains(r.RawName))
                .ToListAsync());
        
        List<string> existingRawValues = results.Select(r => r.RawName).ToList();
        List<string> newRawValues = rawValues.Except(existingRawValues).ToList();
        
        if (newRawValues.Any())
        {
            List<Release> newReleases = newRawValues.Select(rawValue => ParseAndCreateReleaseAsync(projectId, rawValue)).ToList();
            dbContext.Releases.AddRange(newReleases);
            await dbContext.SaveChangesAsync();
            results.AddRange(newReleases);
            
            foreach (Release newRelease in newReleases)
                dbContext.Entry(newRelease).State = EntityState.Detached;
        }
        
        return results;
    }

    private async Task<Release?> GetReleaseByRawValueAsync(int projectId, string rawValue)
    {
        return await dbContext.Releases
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.RawName == rawValue);
    }

    private Release ParseAndCreateReleaseAsync(int projectId, string rawValue)
    {
        (string? package, string? version, int? build, string? commitSha) = ParseReleaseComponents(rawValue);

        return CreateReleaseAsync(projectId, rawValue, package, version, build, commitSha);
    }


    private Release CreateReleaseAsync(int projectId, string rawValue, string? package = null, string? version = null, int? build = null, string? commitSha = null)
    {
        var release = new Release
        {
            ProjectId = projectId,
            RawName = rawValue,
            Package = package,
            SemanticVersion = version,
            Build = build,
            CommitSha = commitSha,
            FirstSeenAt = dateTime.UtcNow
        };

        dbContext.Releases.Add(release);
        return release;
    }

    private static (string? package, string? version, int? build, string? commitSha) ParseReleaseComponents(string rawValue)
    {
        // Extract components from the raw value
        string? package = null;
        string? version = null;
        int? build = null;
        string? commitSha = null;

        // Try to extract package name
        var packageMatch = PackageRegex().Match(rawValue);
        if (packageMatch.Success)
        {
            package = packageMatch.Groups["package"].Value;
        }

        // Try to extract semantic version
        var semVerMatch = SemVerRegex().Match(rawValue);
        if (semVerMatch.Success)
        {
            version = semVerMatch.Value;
        }

        // Try to extract build number
        var buildMatch = BuildNumberRegex().Match(rawValue);
        if (buildMatch.Success && int.TryParse(buildMatch.Groups["build"].Value, out var buildNum))
        {
            build = buildNum;
        }

        // Try to extract commit SHA
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
