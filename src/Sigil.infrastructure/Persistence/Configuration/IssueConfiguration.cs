using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence.Configuration;

internal class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.HasIndex(e => new { e.ProjectId, e.Fingerprint }).IsUnique();
    }
}