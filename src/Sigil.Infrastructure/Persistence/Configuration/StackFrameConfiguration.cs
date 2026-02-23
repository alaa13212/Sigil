using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class StackFrameConfiguration : IEntityTypeConfiguration<StackFrame>
{
    public void Configure(EntityTypeBuilder<StackFrame> builder)
    {
        builder.HasKey(e => e.Id);
    }
}