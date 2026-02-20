using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence.Configuration;

internal class MergeSetConfiguration : IEntityTypeConfiguration<MergeSet>
{
    public void Configure(EntityTypeBuilder<MergeSet> builder)
    {
        builder.HasIndex(m => m.ProjectId);

        builder.HasOne(m => m.PrimaryIssue)
            .WithMany()
            .HasForeignKey(m => m.PrimaryIssueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Issues)
            .WithOne(i => i.MergeSet)
            .HasForeignKey(i => i.MergeSetId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
