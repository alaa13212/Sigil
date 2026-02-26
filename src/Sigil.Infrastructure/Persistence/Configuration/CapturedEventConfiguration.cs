using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class CapturedEventConfiguration : IEntityTypeConfiguration<CapturedEvent>
{
    public void Configure(EntityTypeBuilder<CapturedEvent> builder)
    {
        builder.HasIndex(e => e.EventId).IsUnique();
        builder.HasIndex(e => new { e.IssueId, e.Timestamp });
        
        builder.Property(e => e.Extra)
            .HasColumnType("jsonb");
        
        builder
            .HasMany(e => e.Tags)
            .WithMany(t => t.Events)
            .UsingEntity<EventTag>();
    }
}

