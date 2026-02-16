using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence.Configuration;

internal class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.HasKey(e => new { e.UserId, e.TeamId });
    }
}