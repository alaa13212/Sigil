using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

[ExcludeFromCodeCoverage]
internal class StackTraceFilterConfiguration : IEntityTypeConfiguration<StackTraceFilter>
{
    public void Configure(EntityTypeBuilder<StackTraceFilter> builder)
    {
        builder.HasIndex(f => f.ProjectId);
    }
}
