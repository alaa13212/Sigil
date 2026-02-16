using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IReleaseService
{
    Task<Release> CreateReleaseAsync(int projectId, string rawValue);
    Task<List<Release>> BulkGetOrCreateReleasesAsync(int projectId, IEnumerable<string> rawValues);
}