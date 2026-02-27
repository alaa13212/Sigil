using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class UserIssueStateConfiguration : IEntityTypeConfiguration<UserIssueState>
{
    public void Configure(EntityTypeBuilder<UserIssueState> builder)
    {
        builder.HasKey(s => new { s.UserId, s.IssueId });

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Issue)
            .WithMany()
            .HasForeignKey(s => s.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
