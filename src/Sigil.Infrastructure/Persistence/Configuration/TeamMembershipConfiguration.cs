using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.HasKey(e => new { e.UserId, e.TeamId });

        builder.HasOne<SamlConnection>()
            .WithMany()
            .HasForeignKey(e => e.SamlConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}