using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceMaps;

namespace Sigil.Server.Client.Services;

public class ApiSourceMapService(HttpClient http) : ISourceMapService
{
    public Task<SourceMapUploadResult> UploadAsync(int projectId, string releaseName, string minifiedFilePath, Stream mapStream)
        => throw new NotSupportedException("Direct upload is not supported from the client; use curl or CI integration.");

    public async Task<List<SourceMapInfo>> ListAsync(int releaseId)
        => throw new NotSupportedException("Use ListByReleaseNameAsync from the client.");

    public async Task<List<SourceMapInfo>> ListByReleaseNameAsync(int projectId, string releaseName)
    {
        var encoded = Uri.EscapeDataString(releaseName);
        return await http.GetFromJsonAsync<List<SourceMapInfo>>(
            $"api/projects/{projectId}/releases/{encoded}/sourcemaps") ?? [];
    }

    public async Task<bool> DeleteAsync(int sourceMapId)
    {
        // sourceMapId without release context — handled via separate endpoint if needed
        var response = await http.DeleteAsync($"api/sourcemaps/{sourceMapId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> HasSourceMapsAsync(int releaseId)
        => throw new NotSupportedException("HasSourceMapsAsync is server-side only.");
}
