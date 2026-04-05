using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class ExternalIssueLinkConfiguration : IEntityTypeConfiguration<ExternalIssueLink>
{
    public void Configure(EntityTypeBuilder<ExternalIssueLink> builder)
    {
        builder.HasIndex(e => new { e.IssueId, e.TrackerType }).IsUnique();
        builder.HasIndex(e => e.ExternalId);
        builder.HasOne(e => e.Issue)
            .WithMany(i => i.ExternalLinks)
            .HasForeignKey(e => e.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
