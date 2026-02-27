using Sigil.Application.Models;

namespace Sigil.Application.Interfaces;

public interface IBadgeService
{
    Task<Dictionary<int, ProjectBadgeCounts>> GetAllBadgeCountsAsync(Guid userId);
}
