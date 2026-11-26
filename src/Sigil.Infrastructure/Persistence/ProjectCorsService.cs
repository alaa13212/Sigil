using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;
using Sigil.Domain;
using Sigil.Domain.Cors;

namespace Sigil.Infrastructure.Persistence;

internal class ProjectCorsService(IProjectConfigEditorService configEditor) : IProjectCorsService
{
    public async Task<IReadOnlyList<string>> GetOriginsAsync(int projectId)
    {
        var config = await configEditor.GetAllAsync(projectId);
        return CorsOrigins.Parse(config.GetValueOrDefault(ProjectConfigKeys.AllowedCorsOrigins));
    }

    public async Task<IReadOnlyList<string>> SetOriginsAsync(int projectId, SetCorsOriginsRequest request)
    {
        List<string> origins = [];
        List<string> invalid = [];

        foreach (string part in CorsOrigins.Split(request.Origins))
        {
            string? normalized = CorsOrigins.Normalize(part);

            if (normalized is null)
                invalid.Add(part);
            else if (!origins.Contains(normalized, StringComparer.Ordinal))
                origins.Add(normalized);
        }

        if (invalid.Count > 0)
            throw new InvalidOperationException(
                $"Invalid origin(s): {string.Join(", ", invalid)}. Use scheme://host[:port] with http or https.");

        string? value = origins.Count == 0 ? null : string.Join(",", origins);

        await configEditor.SetAsync(projectId, ProjectConfigKeys.AllowedCorsOrigins, value);

        return origins;
    }
}
