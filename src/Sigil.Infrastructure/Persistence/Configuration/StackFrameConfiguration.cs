using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence.Configuration;

internal class StackFrameConfiguration : IEntityTypeConfiguration<StackFrame>
{
    public void Configure(EntityTypeBuilder<StackFrame> builder)
    {
        builder.HasKey(e => e.Id);
    }
}