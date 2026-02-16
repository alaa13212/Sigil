using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence.Configuration;

internal class ReleaseConfiguration : IEntityTypeConfiguration<Release>
{
    public void Configure(EntityTypeBuilder<Release> builder)
    {
        builder.HasIndex(e => e.RawName).IsUnique();
    }
}