using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceMaps;
using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Services;

internal class SourceMapResolver(
    IServiceProvider serviceProvider,
    IMemoryCache cache) : ISourceMapResolver
{
    public async Task<ResolvedFrame?> ResolveAsync(int releaseId, string filename, int line, int column)
    {
        var cacheKey = $"smap:{releaseId}:{NormalizePath(filename)}";
        if (!cache.TryGetValue(cacheKey, out SourceMapParser? parser))
        {
            parser = await LoadParserAsync(releaseId, filename);
            var opts = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(30))
                .SetSize(1);
            cache.Set(cacheKey, parser, opts);
        }

        if (parser == null) return null;

        // Source map uses 0-based lines; our data is 1-based
        var result = parser.GetOriginalPosition(line - 1, column);
        if (result == null) return null;

        var (origFilename, origLine, origCol, origFunc) = result.Value;
        if (origFilename == null) return null;

        return new ResolvedFrame(origFilename, origLine + 1, origCol, origFunc);
    }

    private async Task<SourceMapParser?> LoadParserAsync(int releaseId, string filename)
    {
        var normalizedFilename = NormalizePath(filename);

        // Use a scope to access scoped DbContext from the singleton resolver
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
        var compression = scope.ServiceProvider.GetRequiredService<ICompressionService>();

        var maps = await dbContext.SourceMaps
            .Where(s => s.ReleaseId == releaseId)
            .Select(s => new { s.MinifiedFilePath, s.CompressedMapData })
            .ToListAsync();

        var match = maps.FirstOrDefault(m => NormalizePath(m.MinifiedFilePath) == normalizedFilename);
        if (match == null) return null;

        var json = compression.DecompressToString(match.CompressedMapData);
        return SourceMapParser.Parse(json);
    }

    private static string NormalizePath(string path)
    {
        // Strip protocol+host: https://example.com/static/js/main.js → /static/js/main.js
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
            path = uri.PathAndQuery;

        // Normalize ~/ prefix
        if (path.StartsWith("~/"))
            path = path[1..];

        return path.TrimStart('/').ToLowerInvariant();
    }
}
