using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class DigestBatchMetricConfiguration : IEntityTypeConfiguration<DigestBatchMetric>
{
    public void Configure(EntityTypeBuilder<DigestBatchMetric> builder)
    {
        builder.HasIndex(e => e.RecordedAt);
        
    }
}