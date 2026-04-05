using Sigil.Application.Models.SourceMaps;

namespace Sigil.Application.Interfaces;

public interface ISourceMapResolver
{
    Task<ResolvedFrame?> ResolveAsync(int releaseId, string filename, int line, int column);
}
