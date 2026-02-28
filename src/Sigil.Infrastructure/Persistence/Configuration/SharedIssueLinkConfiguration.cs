using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class SharedIssueLinkConfiguration : IEntityTypeConfiguration<SharedIssueLink>
{
    public void Configure(EntityTypeBuilder<SharedIssueLink> builder)
    {
        builder.HasKey(l => l.Token);

        builder.HasOne(l => l.Issue)
            .WithMany()
            .HasForeignKey(l => l.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.CreatedBy)
            .WithMany()
            .HasForeignKey(l => l.CreatedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.ExpiresAt);
    }
}
