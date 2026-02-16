using Sigil.Application.Interfaces;

namespace Sigil.infrastructure.Services;

internal class DateTimeProvider : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}