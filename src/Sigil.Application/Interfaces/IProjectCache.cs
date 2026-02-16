using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IProjectCache : ICacheService
{
    static string ICacheService.CategoryName => "projects";

    Task<Project?> GetProjectById(int id);
}