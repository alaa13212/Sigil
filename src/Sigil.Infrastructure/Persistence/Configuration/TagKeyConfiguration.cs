using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class TagKeyConfiguration : IEntityTypeConfiguration<TagKey>
{
    public void Configure(EntityTypeBuilder<TagKey> builder)
    {
        builder.HasIndex(e => e.Key).IsUnique();
    }
}