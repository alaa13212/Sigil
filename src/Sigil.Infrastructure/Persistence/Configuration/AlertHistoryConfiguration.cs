using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class AlertHistoryConfiguration : IEntityTypeConfiguration<AlertHistory>
{
    public void Configure(EntityTypeBuilder<AlertHistory> builder)
    {
        builder.HasIndex(h => h.AlertRuleId);
        builder.HasIndex(h => h.FiredAt);

        builder.HasOne(h => h.AlertRule)
            .WithMany()
            .HasForeignKey(h => h.AlertRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.Issue)
            .WithMany()
            .HasForeignKey(h => h.IssueId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
