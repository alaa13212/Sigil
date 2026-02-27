using Sigil.Application.Models.Search;

namespace Sigil.Application.Interfaces;

public interface ISearchService
{
    Task<SearchResultsResponse> SearchAsync(string query, int? projectId);
}
