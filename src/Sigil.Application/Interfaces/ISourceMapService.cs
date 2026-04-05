using Sigil.Application.Models.SourceMaps;

namespace Sigil.Application.Interfaces;

public interface ISourceMapService
{
    Task<SourceMapUploadResult> UploadAsync(int projectId, string releaseName, string minifiedFilePath, Stream mapStream);
    Task<List<SourceMapInfo>> ListAsync(int releaseId);
    Task<List<SourceMapInfo>> ListByReleaseNameAsync(int projectId, string releaseName);
    Task<bool> DeleteAsync(int sourceMapId);
    Task<bool> HasSourceMapsAsync(int releaseId);
}
