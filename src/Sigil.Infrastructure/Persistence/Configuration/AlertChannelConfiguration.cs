using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence.Configuration;

internal class AlertChannelConfiguration : IEntityTypeConfiguration<AlertChannel>
{
    public void Configure(EntityTypeBuilder<AlertChannel> builder)
    {
    }
}
