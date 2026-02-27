using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Filters;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class StackTraceFilterService(SigilDbContext dbContext, IStackTraceFilterCache cache, IDateTime dateTime) : IStackTraceFilterService
{
    public async Task<List<StackTraceFilterResponse>> GetFiltersAsync(int projectId)
    {
        List<StackTraceFilter> filters = await GetRawFiltersForProjectAsync(projectId);
        return filters.Select(ToResponse).ToList();
    }

    public async Task<StackTraceFilterResponse> CreateFilterAsync(int projectId, CreateStackTraceFilterRequest request)
    {
        var filter = new StackTraceFilter
        {
            ProjectId = projectId,
            Field = request.Field,
            Operator = request.Operator,
            Value = request.Value,
            Enabled = request.Enabled,
            Priority = request.Priority,
            Description = request.Description,
            CreatedAt = dateTime.UtcNow,
        };

        dbContext.StackTraceFilters.Add(filter);
        await dbContext.SaveChangesAsync();
        cache.Invalidate(projectId);
        return ToResponse(filter);
    }

    public async Task<StackTraceFilterResponse?> UpdateFilterAsync(int filterId, UpdateStackTraceFilterRequest request)
    {
        var filter = await dbContext.StackTraceFilters.AsTracking().FirstOrDefaultAsync(f => f.Id == filterId);
        if (filter is null) return null;

        filter.Field = request.Field;
        filter.Operator = request.Operator;
        filter.Value = request.Value;
        filter.Enabled = request.Enabled;
        filter.Priority = request.Priority;
        filter.Description = request.Description;

        await dbContext.SaveChangesAsync();
        cache.Invalidate(filter.ProjectId);
        return ToResponse(filter);
    }

    public async Task<bool> DeleteFilterAsync(int filterId)
    {
        var filter = await dbContext.StackTraceFilters.FirstOrDefaultAsync(f => f.Id == filterId);
        if (filter is null) return false;

        await dbContext.StackTraceFilters.Where(f => f.Id == filterId).ExecuteDeleteAsync();
        cache.Invalidate(filter.ProjectId);
        return true;
    }

    public async Task<List<StackTraceFilter>> GetRawFiltersForProjectAsync(int projectId)
    {
        if (cache.TryGet(projectId, out var cached) && cached is not null)
            return cached;

        var filters = await dbContext.StackTraceFilters
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Priority)
            .ToListAsync();

        cache.Set(projectId, filters);
        return filters;
    }

    private static StackTraceFilterResponse ToResponse(StackTraceFilter f) =>
        new(f.Id, f.ProjectId, f.Field, f.Operator, f.Value, f.Enabled, f.Priority, f.Description, f.CreatedAt);
}
