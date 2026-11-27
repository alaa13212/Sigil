using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Tests.Fixtures;

/// <summary>Resolves a fixed set of visible project ids.</summary>
public sealed class StubProjectAccessService(params int[] projectIds) : IProjectAccessService
{
    private readonly List<int> _projectIds = [.. projectIds];

    public Task<List<int>> GetVisibleProjectIdsAsync() => Task.FromResult(_projectIds);
}
