using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Services;

internal class DateTimeProvider : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}