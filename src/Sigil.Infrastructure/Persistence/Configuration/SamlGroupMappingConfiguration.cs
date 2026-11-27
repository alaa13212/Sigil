using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

[ExcludeFromCodeCoverage]
internal class SamlGroupMappingConfiguration : IEntityTypeConfiguration<SamlGroupMapping>
{
    public void Configure(EntityTypeBuilder<SamlGroupMapping> builder)
    {
        builder.ToTable("SamlGroupMappings");
    }
}
