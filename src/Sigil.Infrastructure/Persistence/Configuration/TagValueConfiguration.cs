using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class TagValueConfiguration : IEntityTypeConfiguration<TagValue>
{
    public void Configure(EntityTypeBuilder<TagValue> builder)
    {
        builder.HasIndex(e => new { e.TagKeyId, e.Value }).IsUnique();
    }
}