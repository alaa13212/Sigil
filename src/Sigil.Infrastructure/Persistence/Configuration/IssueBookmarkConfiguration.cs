using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class IssueBookmarkConfiguration : IEntityTypeConfiguration<IssueBookmark>
{
    public void Configure(EntityTypeBuilder<IssueBookmark> builder)
    {
        builder.HasKey(b => new { b.UserId, b.IssueId });

        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Issue)
            .WithMany()
            .HasForeignKey(b => b.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
