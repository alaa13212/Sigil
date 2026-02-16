using System.Security.Cryptography;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence;

internal class ProjectService(SigilDbContext dbContext) : IProjectService
{
    public async Task<Project> CreateProjectAsync(string name, Platform platform)
    {
        var project = new Project
        {
            Name = name,
            Platform = platform,
            ApiKey = RandomNumberGenerator.GetHexString(32)
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        return project;
    }

    public async Task<Project?> GetProjectByIdAsync(int id)
    {
        return await dbContext.Projects.FindAsync(id);
    }
}
