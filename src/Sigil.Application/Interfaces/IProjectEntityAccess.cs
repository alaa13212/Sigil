using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Direct entity access for internal use (digestion pipeline, authorization).</summary>
public interface IProjectEntityAccess
{
    Task<Project?> GetProjectByIdAsync(int id);
    Task<List<Project>> GetAllProjectsAsync();
}
