using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence.Configuration;

internal class EventFilterConfiguration : IEntityTypeConfiguration<EventFilter>
{
    public void Configure(EntityTypeBuilder<EventFilter> builder)
    {
        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => new { e.ProjectId, e.Priority });
    }
}
