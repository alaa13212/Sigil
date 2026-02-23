using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class EventUserConfiguration : IEntityTypeConfiguration<EventUser>
{
    public void Configure(EntityTypeBuilder<EventUser> builder)
    {
        builder.HasIndex(e => e.UniqueIdentifier).IsUnique();
    }
}