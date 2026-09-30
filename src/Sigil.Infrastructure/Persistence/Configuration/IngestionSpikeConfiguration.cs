using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

[ExcludeFromCodeCoverage]
internal class IngestionSpikeConfiguration : IEntityTypeConfiguration<IngestionSpike>
{
    public void Configure(EntityTypeBuilder<IngestionSpike> builder)
    {
        builder.HasIndex(s => s.ProjectId);
        builder.HasIndex(s => s.StartedAt);
    }
}
