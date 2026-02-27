using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Issues;

namespace Sigil.Server.Client.Services;

public class ApiBookmarkService(HttpClient http) : IBookmarkService
{
    public async Task<bool> ToggleBookmarkAsync(int issueId, Guid userId)
    {
        var response = await http.PostAsync($"api/issues/{issueId}/bookmark", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BookmarkStatusResult>();
        return result?.IsBookmarked ?? false;
    }

    public async Task<bool> IsBookmarkedAsync(int issueId, Guid userId)
    {
        var result = await http.GetFromJsonAsync<BookmarkStatusResult>($"api/issues/{issueId}/bookmark");
        return result?.IsBookmarked ?? false;
    }

    public async Task<List<IssueSummary>> GetBookmarkedIssuesAsync(Guid userId)
    {
        return await http.GetFromJsonAsync<List<IssueSummary>>("api/bookmarks") ?? [];
    }

    public Task RecordIssueViewAsync(int issueId, Guid userId) => Task.CompletedTask;

    private record BookmarkStatusResult(bool IsBookmarked);
}
