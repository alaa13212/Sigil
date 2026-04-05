using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class SourceMapConfiguration : IEntityTypeConfiguration<SourceMap>
{
    public void Configure(EntityTypeBuilder<SourceMap> builder)
    {
        builder.HasIndex(e => new { e.ReleaseId, e.MinifiedFilePath }).IsUnique();
        builder.HasOne(e => e.Release)
            .WithMany(r => r.SourceMaps)
            .HasForeignKey(e => e.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
