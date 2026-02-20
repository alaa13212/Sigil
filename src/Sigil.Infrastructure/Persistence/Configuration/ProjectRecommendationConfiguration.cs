using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence.Configuration;

internal class ProjectRecommendationConfiguration : IEntityTypeConfiguration<ProjectRecommendation>
{
    public void Configure(EntityTypeBuilder<ProjectRecommendation> builder)
    {
        builder.HasIndex(r => r.ProjectId);
        builder.HasIndex(r => new { r.ProjectId, r.AnalyzerId }).IsUnique();

        builder.HasOne(r => r.Project)
            .WithMany()
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
