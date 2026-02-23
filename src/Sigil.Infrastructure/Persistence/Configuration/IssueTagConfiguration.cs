using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class IssueTagConfiguration : IEntityTypeConfiguration<IssueTag>
{
    public void Configure(EntityTypeBuilder<IssueTag> builder)
    {
        builder.HasKey(e => new { e.IssueId, e.TagValueId });
        builder.HasIndex(e => e.TagValueId);
    }
}