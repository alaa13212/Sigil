namespace Sigil.Application.Models.SourceMaps;

public record SourceMapUploadResult(int Id, string MinifiedFilePath, long OriginalSize);

public record SourceMapInfo(int Id, string MinifiedFilePath, long OriginalSize, DateTime UploadedAt);

public record ResolvedFrame(string OriginalFilename, int OriginalLine, int OriginalColumn, string? OriginalFunction);
