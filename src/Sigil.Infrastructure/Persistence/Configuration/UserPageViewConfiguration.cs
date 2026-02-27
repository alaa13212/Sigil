using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class UserPageViewConfiguration : IEntityTypeConfiguration<UserPageView>
{
    public void Configure(EntityTypeBuilder<UserPageView> builder)
    {
        builder.HasKey(v => new { v.UserId, v.ProjectId, v.PageType });

        builder.HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Project)
            .WithMany()
            .HasForeignKey(v => v.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
