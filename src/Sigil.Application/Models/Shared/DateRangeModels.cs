namespace Sigil.Application.Models.Shared;

/// <summary>
/// A time range expressed as UTC instants. Either bound may be open; both bounds are inclusive.
/// </summary>
public record DateRange(DateTimeOffset? Since, DateTimeOffset? Until)
{
    public static readonly DateRange Empty = new(null, null);

    /// <summary>True when neither end of the range is bounded.</summary>
    public bool IsEmpty => Since is null && Until is null;
}
