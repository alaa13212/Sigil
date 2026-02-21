using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class TextNormalizationRuleConfiguration : IEntityTypeConfiguration<TextNormalizationRule>
{
    public void Configure(EntityTypeBuilder<TextNormalizationRule> builder)
    {
        builder.HasIndex(r => new { r.ProjectId, r.Priority });

        builder.HasOne(r => r.Project)
            .WithMany(p => p.Rules)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
