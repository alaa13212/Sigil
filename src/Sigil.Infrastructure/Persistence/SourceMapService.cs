using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceMaps;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class SourceMapService(SigilDbContext dbContext, ICompressionService compression, IDateTime dateTime) : ISourceMapService
{
    private const long MaxFileSizeBytes = 50L * 1024 * 1024;   // 50 MB
    private const long MaxTotalSizeBytes = 200L * 1024 * 1024; // 200 MB

    public async Task<SourceMapUploadResult> UploadAsync(
        int projectId, string releaseName, string minifiedFilePath, Stream mapStream)
    {
        // Read stream
        using var ms = new MemoryStream();
        await mapStream.CopyToAsync(ms);
        var rawBytes = ms.ToArray();

        if (rawBytes.LongLength > MaxFileSizeBytes)
            throw new InvalidOperationException($"Source map exceeds maximum size of {MaxFileSizeBytes / 1024 / 1024} MB.");

        var rawText = System.Text.Encoding.UTF8.GetString(rawBytes);
        ValidateSourceMap(rawText);

        var release = await GetOrCreateReleaseAsync(projectId, releaseName);

        // Enforce total per-release size
        var totalExisting = await dbContext.SourceMaps
            .Where(s => s.ReleaseId == release.Id)
            .SumAsync(s => s.OriginalSize);

        if (totalExisting + rawBytes.LongLength > MaxTotalSizeBytes)
            throw new InvalidOperationException("Uploading this source map would exceed the 200 MB per-release limit.");

        var compressed = compression.CompressString(rawText);

        // Upsert on (ReleaseId, MinifiedFilePath)
        var existing = await dbContext.SourceMaps
            .FirstOrDefaultAsync(s => s.ReleaseId == release.Id && s.MinifiedFilePath == minifiedFilePath);

        SourceMap sourceMap;
        if (existing != null)
        {
            existing.CompressedMapData = compressed;
            existing.OriginalSize = rawBytes.LongLength;
            existing.UploadedAt = dateTime.UtcNow;
            dbContext.SourceMaps.Update(existing);
            sourceMap = existing;
        }
        else
        {
            sourceMap = new SourceMap
            {
                ReleaseId = release.Id,
                MinifiedFilePath = minifiedFilePath,
                OriginalSize = rawBytes.LongLength,
                CompressedMapData = compressed,
                UploadedAt = dateTime.UtcNow
            };
            dbContext.SourceMaps.Add(sourceMap);
        }

        await dbContext.SaveChangesAsync();
        return new SourceMapUploadResult(sourceMap.Id, sourceMap.MinifiedFilePath, sourceMap.OriginalSize);
    }

    public async Task<List<SourceMapInfo>> ListAsync(int releaseId)
    {
        return await dbContext.SourceMaps
            .Where(s => s.ReleaseId == releaseId)
            .OrderBy(s => s.MinifiedFilePath)
            .Select(s => new SourceMapInfo(s.Id, s.MinifiedFilePath, s.OriginalSize, s.UploadedAt))
            .ToListAsync();
    }

    public async Task<List<SourceMapInfo>> ListByReleaseNameAsync(int projectId, string releaseName)
    {
        return await dbContext.SourceMaps
            .Where(s => s.Release!.ProjectId == projectId && s.Release.RawName == releaseName)
            .OrderBy(s => s.MinifiedFilePath)
            .Select(s => new SourceMapInfo(s.Id, s.MinifiedFilePath, s.OriginalSize, s.UploadedAt))
            .ToListAsync();
    }

    public async Task<bool> DeleteAsync(int sourceMapId)
    {
        var deleted = await dbContext.SourceMaps.Where(s => s.Id == sourceMapId).ExecuteDeleteAsync();
        return deleted > 0;
    }

    public async Task<bool> HasSourceMapsAsync(int releaseId)
    {
        return await dbContext.SourceMaps.AnyAsync(s => s.ReleaseId == releaseId);
    }

    private static void ValidateSourceMap(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("mappings", out _))
                throw new InvalidOperationException("Invalid source map: missing \"mappings\" field.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Invalid source map: file is not valid JSON.");
        }
    }

    private async Task<Release> GetOrCreateReleaseAsync(int projectId, string releaseName)
    {
        var release = await dbContext.Releases
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.RawName == releaseName);

        if (release != null)
            return release;

        release = new Release
        {
            ProjectId = projectId,
            RawName = releaseName,
            FirstSeenAt = dateTime.UtcNow
        };

        dbContext.Releases.Add(release);
        await dbContext.SaveChangesAsync();
        return release;
    }
}
