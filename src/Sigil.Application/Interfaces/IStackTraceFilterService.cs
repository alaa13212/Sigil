using Sigil.Application.Models.Filters;

namespace Sigil.Application.Interfaces;

public interface IStackTraceFilterService
{
    Task<List<StackTraceFilterResponse>> GetFiltersAsync(int projectId);
    Task<StackTraceFilterResponse> CreateFilterAsync(int projectId, CreateStackTraceFilterRequest request);
    Task<StackTraceFilterResponse?> UpdateFilterAsync(int filterId, UpdateStackTraceFilterRequest request);
    Task<bool> DeleteFilterAsync(int filterId);
}
