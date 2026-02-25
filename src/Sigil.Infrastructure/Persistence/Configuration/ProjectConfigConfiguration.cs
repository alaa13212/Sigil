using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class ProjectConfigConfiguration : IEntityTypeConfiguration<ProjectConfig>
{
    public void Configure(EntityTypeBuilder<ProjectConfig> builder)
    {
        builder.HasKey(c => new { c.ProjectId, c.Key });

        builder.HasOne(c => c.Project)
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
