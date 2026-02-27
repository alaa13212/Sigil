using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class EventBucketConfiguration : IEntityTypeConfiguration<EventBucket>
{
    public void Configure(EntityTypeBuilder<EventBucket> builder)
    {
        builder.HasKey(b => new { b.IssueId, b.BucketStart });

        builder.HasOne(b => b.Issue)
            .WithMany()
            .HasForeignKey(b => b.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
