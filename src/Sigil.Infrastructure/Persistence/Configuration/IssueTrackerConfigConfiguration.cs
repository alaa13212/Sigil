using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class IssueTrackerConfigConfiguration : IEntityTypeConfiguration<IssueTrackerConfig>
{
    public void Configure(EntityTypeBuilder<IssueTrackerConfig> builder)
    {
        builder.HasIndex(e => new { e.ProjectId, e.TrackerType }).IsUnique();
        builder.HasOne(e => e.Project)
            .WithMany()
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
