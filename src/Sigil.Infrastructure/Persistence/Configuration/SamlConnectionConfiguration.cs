using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

[ExcludeFromCodeCoverage]
internal class SamlConnectionConfiguration : IEntityTypeConfiguration<SamlConnection>
{
    public void Configure(EntityTypeBuilder<SamlConnection> builder)
    {
        builder.ToTable("SamlConnections");

        builder.HasMany(c => c.GroupMappings)
            .WithOne(m => m.SamlConnection)
            .HasForeignKey(m => m.SamlConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Users)
            .WithOne(u => u.SamlConnection)
            .HasForeignKey(u => u.SamlConnectionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
