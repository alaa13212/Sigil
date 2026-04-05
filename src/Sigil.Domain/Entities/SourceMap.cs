using System.ComponentModel.DataAnnotations;

namespace Sigil.Domain.Entities;

public class SourceMap
{
    public int Id { get; set; }

    public int ReleaseId { get; set; }
    public Release? Release { get; set; }

    [MaxLength(1000)]
    public required string MinifiedFilePath { get; set; }

    public long OriginalSize { get; set; }

    public required byte[] CompressedMapData { get; set; }

    public DateTime UploadedAt { get; set; }
}
