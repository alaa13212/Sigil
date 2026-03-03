using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Provides raw stack trace filters to the digestion pipeline.</summary>
public interface IStackTraceFilterSource
{
    Task<List<StackTraceFilter>> GetRawFiltersForProjectAsync(int projectId);
}
