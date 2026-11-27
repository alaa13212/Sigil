namespace Sigil.Application.Interfaces;

/// <summary>Resolves the projects the current user is allowed to see.</summary>
public interface IProjectAccessService
{
    /// <summary>Ids of the projects the current user may see, in no particular order.</summary>
    Task<List<int>> GetVisibleProjectIdsAsync();
}
