using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence.Configuration;

internal class FailedEventConfiguration : IEntityTypeConfiguration<FailedEvent>
{
    public void Configure(EntityTypeBuilder<FailedEvent> builder)
    {
        builder.HasIndex(e => new { e.ProjectId, e.Reprocessed });
    }
}
