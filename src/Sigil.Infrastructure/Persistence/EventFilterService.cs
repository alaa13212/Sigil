using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Filters;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.infrastructure.Persistence;

internal class EventFilterService(SigilDbContext dbContext, IEventFilterCache filterCache, IDateTime dateTime) : IEventFilterService
{
    public async Task<List<EventFilterResponse>> GetFiltersAsync(int projectId)
    {
        List<EventFilter> filters = await GetRawFiltersForProjectAsync(projectId);
        return filters.Select(ToResponse).ToList();
    }

    public async Task<EventFilterResponse> CreateFilterAsync(int projectId, CreateFilterRequest request)
    {
        var filter = new EventFilter
        {
            ProjectId = projectId,
            Field = request.Field,
            Operator = request.Operator,
            Value = request.Value,
            Action = request.Action,
            Enabled = request.Enabled,
            Priority = request.Priority,
            Description = request.Description,
            CreatedAt = dateTime.UtcNow,
        };

        dbContext.EventFilters.Add(filter);
        await dbContext.SaveChangesAsync();
        filterCache.Invalidate(projectId);
        return ToResponse(filter);
    }

    public async Task<EventFilterResponse?> UpdateFilterAsync(int filterId, UpdateFilterRequest request)
    {
        var filter = await dbContext.EventFilters.AsTracking().FirstOrDefaultAsync(f => f.Id == filterId);
        if (filter is null) return null;

        filter.Field = request.Field;
        filter.Operator = request.Operator;
        filter.Value = request.Value;
        filter.Action = request.Action;
        filter.Enabled = request.Enabled;
        filter.Priority = request.Priority;
        filter.Description = request.Description;

        await dbContext.SaveChangesAsync();
        filterCache.Invalidate(filter.ProjectId);
        return ToResponse(filter);
    }

    public async Task<bool> DeleteFilterAsync(int filterId)
    {
        var filter = await dbContext.EventFilters.FirstOrDefaultAsync(f => f.Id == filterId);
        if (filter is null) return false;

        await dbContext.EventFilters.Where(f => f.Id == filterId).ExecuteDeleteAsync();
        filterCache.Invalidate(filter.ProjectId);
        return true;
    }

    public async Task<List<EventFilter>> GetRawFiltersForProjectAsync(int projectId)
    {
        if (filterCache.TryGet(projectId, out var cached) && cached is not null)
            return cached;

        var filters = await dbContext.EventFilters
            .Where(f => f.ProjectId == projectId)
            .OrderBy(f => f.Priority)
            .ToListAsync();

        filterCache.Set(projectId, filters);
        return filters;
    }

    public bool ShouldRejectEvent(ParsedEvent parsedEvent, List<EventFilter> filters)
    {
        foreach (var filter in filters.Where(f => f.Enabled))
        {
            var fieldValue = GetFieldValue(parsedEvent, filter.Field);
            if (fieldValue is null) continue;

            bool matches = filter.Operator switch
            {
                FilterOperator.Equals     => string.Equals(fieldValue, filter.Value, StringComparison.OrdinalIgnoreCase),
                FilterOperator.Contains   => fieldValue.Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
                FilterOperator.StartsWith => fieldValue.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
                FilterOperator.EndsWith   => fieldValue.EndsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
                FilterOperator.Regex      => IsRegexMatch(fieldValue, filter.Value),
                _ => false
            };

            if (matches && filter.Action == FilterAction.Reject)
                return true;
        }

        return false;
    }

    private static string? GetFieldValue(ParsedEvent e, string field) => field switch
    {
        "exceptionType" => e.ExceptionType,
        "message"       => e.Message ?? e.NormalizedMessage,
        "release"       => e.Release,
        "environment"   => e.Environment,
        "logger"        => e.Logger,
        "fingerprint"   => e.Fingerprint,
        _ when field.StartsWith("tag:") => e.Tags?.GetValueOrDefault(field[4..]),
        _ => null
    };

    private static bool IsRegexMatch(string input, string pattern)
    {
        try { return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)); }
        catch { return false; }
    }

    private static EventFilterResponse ToResponse(EventFilter f) =>
        new(f.Id, f.ProjectId, f.Field, f.Operator, f.Value, f.Action, f.Enabled, f.Priority, f.Description, f.CreatedAt);
}
