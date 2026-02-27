using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Search;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class SearchService(SigilDbContext dbContext) : ISearchService
{
    public async Task<SearchResultsResponse> SearchAsync(string query, int? projectId)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new SearchResultsResponse([], [], []);

        string trimmed = query.Trim();
        List<IssueSearchResult> issues = await SearchIssuesAsync(trimmed, projectId);
        List<ReleaseSearchResult> releases = await SearchReleasesAsync(trimmed, projectId);
        List<TagSearchResult> tags = await SearchTagsAsync(trimmed, projectId);

        return new SearchResultsResponse(issues, releases, tags);
    }

    private async Task<List<IssueSearchResult>> SearchIssuesAsync(string query, int? projectId)
    {
        if(query.Length < 1)
            return [];
        
        query = BuildPrefixQuery(query);
        
        IQueryable<Issue> q = dbContext.Issues
            .Where(i => EF.Property<NpgsqlTsVector>(i, "SearchVector").Matches(EF.Functions.ToTsQuery("simple", query)))
            .Where(i => i.MergeSetId == null || i.MergeSet!.PrimaryIssueId == i.Id);

        if (projectId.HasValue)
            q = q.Where(i => i.ProjectId == projectId.Value);

        return await q
            .OrderByDescending(i => EF.Property<NpgsqlTsVector>(i, "SearchVector").Rank(EF.Functions.ToTsQuery("simple", query)))
            .Take(5)
            .Select(i => new IssueSearchResult(
                i.Id, i.ProjectId, i.Title, i.ExceptionType,
                i.Status, i.MergeSet != null ? i.MergeSet.Level : i.Level))
            .ToListAsync();
    }

    private async Task<List<ReleaseSearchResult>> SearchReleasesAsync(string query, int? projectId)
    {
        if(query.Length < 1)
            return [];
        
        IQueryable<Release> q = dbContext.Releases
            .Where(r => r.RawName.Contains(query));

        if (projectId.HasValue)
            q = q.Where(r => r.ProjectId == projectId.Value);

        return await q
            .OrderByDescending(r => r.FirstSeenAt)
            .Take(3)
            .Select(r => new ReleaseSearchResult(r.Id, r.ProjectId, r.RawName))
            .ToListAsync();
    }

    private async Task<List<TagSearchResult>> SearchTagsAsync(string query, int? projectId)
    {
        query = $"%{query}%";
        var q = dbContext.IssueTags
            .Where(t => EF.Functions.ILike(t.TagValue!.TagKey!.Key, query) || EF.Functions.ILike(t.TagValue.Value, query));

        if (projectId.HasValue)
            q = q.Where(t => t.Issue!.ProjectId == projectId.Value);

        return await q
            .GroupBy(t => new
            {
                Key = t.TagValue!.TagKey!.Key,
                Value = t.TagValue.Value,
                ProjectId = t.Issue!.ProjectId,
                ProjectName = t.Issue.Project!.Name
            })
            .OrderByDescending(g => g.Count())
            .Select(g => new TagSearchResult(g.Key.Key, g.Key.Value, g.Key.ProjectId, g.Key.ProjectName, g.Count()))
            .Take(5)
            .ToListAsync();
    }
    
    public static string BuildPrefixQuery(string input)
    {
        return string.Join(" & ",
            input.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(term => $"{term}:*"));
    }
}
