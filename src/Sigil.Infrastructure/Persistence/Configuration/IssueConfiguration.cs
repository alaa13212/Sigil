using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.HasIndex(e => new { e.ProjectId, e.Fingerprint }).IsUnique();
        builder.HasIndex(e => e.MergeSetId);

        builder.HasOne(i => i.IgnoreFilter)
            .WithMany()
            .HasForeignKey(i => i.IgnoreFilterId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .IsRequired(false)
            .HasComputedColumnSql(@"
                setweight(to_tsvector('simple', coalesce(""Title"", '')), 'A') ||
                setweight(to_tsvector('simple', coalesce(""ExceptionType"", '')), 'A') ||
                setweight(to_tsvector('simple', coalesce(""Culprit"", '')), 'B') ||
                setweight(to_tsvector('simple', coalesce(""SuggestedEventMessage"", '')), 'B') ||
                setweight(to_tsvector('simple', coalesce(""SuggestedFramesSummary"", '')), 'C')
            ", stored: true);

        builder.HasIndex("SearchVector")
            .HasMethod("GIN");
    }
}